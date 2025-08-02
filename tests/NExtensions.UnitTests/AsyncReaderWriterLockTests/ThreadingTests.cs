using System.Diagnostics;
using NExtensions.UnitTests.Utilities;
using Shouldly;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public class ThreadingTests
{
	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task ConcurrentReadersAndWriters_ShouldMaintainDataIntegrity(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);
		var sharedCounter = 0;
		const int readerIterations = 1000;
		const int writerIterations = 100;
		const int workers = 10;
		long writingTicks = 0;
		long readingTicks = 0;

		var readers = Enumerable
			.Range(0, workers)
			.Select(_ => Task.Run(async () =>
			{
				var readWatch = new Stopwatch();
				for (var i = 0; i < readerIterations; i++)
				{
					using (await rwLock.EnterReaderScopeAsync())
					{
						readWatch.Start();
						var val1 = sharedCounter;
						await Task.Yield();
						var val2 = sharedCounter;
						val1.ShouldBe(val2);
						readWatch.Stop();
					}
				}

				Interlocked.Add(ref readingTicks, readWatch.ElapsedTicks);
			}));

		var writers = Enumerable
			.Range(0, workers)
			.Select(_ => Task.Run(async () =>
			{
				var writeWatch = new Stopwatch();
				for (var i = 0; i < writerIterations; i++)
				{
					using (await rwLock.EnterWriterScopeAsync())
					{
						writeWatch.Start();
						sharedCounter++;
						await Task.Yield();
						writeWatch.Stop();
					}
				}

				Interlocked.Add(ref writingTicks, writeWatch.ElapsedTicks);
			}));

		// Run all readers and writers concurrently
		var watch = Stopwatch.StartNew();
		await Task.WhenAll(readers.Concat(writers));
		watch.Stop();
		var duration = TimeSpan.FromTicks(readingTicks + writingTicks);
		duration.ShouldBeGreaterThan(watch.Elapsed);
		sharedCounter.ShouldBe(workers * writerIterations);
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task AsyncReaderWriterLock_AllowsSingleWriterAndMultipleReadersConcurrently_WithRandomCancellation(bool syncReader, bool syncWriter)
	{
		const int expectedHits = 500;
		var asyncLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		var concurrentWriteCount = 0;
		var maxConcurrentWrite = 0;
		var totalWriteHits = 0;
		var failedWriteHits = 0;

		var concurrentReadCount = 0;
		var maxConcurrentRead = 0;
		var totalReadHits = 0;
		var failedReadHits = 0;

		var writerParallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = 2
		};
		var writer = ParallelUtility.ForAsync(0, expectedHits, writerParallelOptions, async (_, _) =>
		{
			try
			{
				await Task.Delay(1, CancellationToken.None);
				using var cts = new CancellationTokenSource(Random.Shared.Next(0, 5));
				using (await asyncLock.EnterWriterScopeAsync(cts.Token))
				{
					Interlocked.Increment(ref totalWriteHits);
					var localCount = Interlocked.Increment(ref concurrentWriteCount);
					InterlockedUtility.Max(ref maxConcurrentWrite, localCount);
					await Task.Delay(1, CancellationToken.None);
					Interlocked.Decrement(ref concurrentWriteCount);
				}
			}
			catch (OperationCanceledException)
			{
				Interlocked.Increment(ref failedWriteHits);
			}
		});

		var readerParallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount / 2
		};
		var reader = ParallelUtility.ForAsync(0, expectedHits, readerParallelOptions, async (_, _) =>
		{
			try
			{
				await Task.Delay(1, CancellationToken.None);
				using var cts = new CancellationTokenSource(Random.Shared.Next(0, 5));
				using (await asyncLock.EnterReaderScopeAsync(cts.Token))
				{
					Interlocked.Increment(ref totalReadHits);
					var localCount = Interlocked.Increment(ref concurrentReadCount);
					InterlockedUtility.Max(ref maxConcurrentRead, localCount);
					await Task.Delay(1, CancellationToken.None);
					Interlocked.Decrement(ref concurrentReadCount);
				}
			}
			catch (OperationCanceledException)
			{
				Interlocked.Increment(ref failedReadHits);
			}
		});

		await Task.WhenAll(reader, writer);

		// Assert
		failedWriteHits.ShouldBeGreaterThan(0);
		failedReadHits.ShouldBeGreaterThan(0);
		(totalWriteHits + failedWriteHits).ShouldBe(expectedHits, "All writes should be accounted for.");
		(totalReadHits + failedReadHits).ShouldBe(expectedHits, "All reads should be accounted for.");
		if (failedReadHits != expectedHits)
		{
			if (Environment.ProcessorCount >= 2)
				maxConcurrentRead.ShouldBeGreaterThan(1, "Readers should be able to run concurrently.");
			else
				maxConcurrentRead.ShouldBeGreaterThanOrEqualTo(1, "Readers should be able to run concurrently (but maybe not on small hardware).");
		}
		if (failedWriteHits != expectedHits)
			maxConcurrentWrite.ShouldBe(1, "Writer lock must be exclusive.");
	}
}