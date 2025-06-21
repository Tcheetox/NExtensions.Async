using System.Diagnostics;
using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public class ThreadingTests
{
	[Fact]
	public async Task ConcurrentReadersAndWriters_ShouldMaintainDataIntegrity()
	{
		var rwLock = new AsyncReaderWriterLock();
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
					
					using (await rwLock.ReaderLockAsync())
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
					using (await rwLock.WriterLockAsync())
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
}