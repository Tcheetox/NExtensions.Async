using NExtensions.Async;
using NExtensions.UnitTests.Utilities;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLazyTests;

public class ThreadingTests
{
	[Fact]
	public async Task AsyncReaderWriterLock_AllowsSingleWriterAndMultipleReadersConcurrently_WithRandomCancellation()
	{
		const int expectedHits = 100_000;
		var asyncLock = new AsyncReaderWriterLock();
		var concurrentWriteCount = 0;
		var maxConcurrentWrite = 0;
		var totalWriteHits = 0;
		var failedWriteHits = 0;

		var concurrentReadCount = 0;
		var maxConcurrentRead = 0;
		var totalReadHits = 0;
		var failedReadHits = 0;

		var parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount / 2
		};
		
		var writer = ParallelUtility.ForAsync(0, expectedHits, parallelOptions, async (_, _) =>
		{
			try
			{
				using var cts = new CancellationTokenSource(Random.Shared.Next(0, 15));

				using (await asyncLock.EnterWriterScopeAsync(cts.Token))
				{
					Interlocked.Increment(ref totalWriteHits);
					var localCount = Interlocked.Increment(ref concurrentWriteCount);
					InterlockedUtility.Max(ref maxConcurrentWrite, localCount);
					await Task.Yield();
					Interlocked.Decrement(ref concurrentWriteCount);
				}
			}
			catch (OperationCanceledException)
			{
				Interlocked.Increment(ref failedWriteHits);
			}
		});

		var reader = ParallelUtility.ForAsync(0, expectedHits, parallelOptions, async (_, _) =>
		{
			try
			{
				using var cts = new CancellationTokenSource(Random.Shared.Next(0, 15));

				using (await asyncLock.EnterReaderScopeAsync(cts.Token))
				{
					Interlocked.Increment(ref totalReadHits);
					var localCount = Interlocked.Increment(ref concurrentReadCount);
					InterlockedUtility.Max(ref maxConcurrentRead, localCount);
					await Task.Yield();
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
		maxConcurrentRead.ShouldBeGreaterThan(1, "Readers should be able to run concurrently.");
		maxConcurrentWrite.ShouldBe(1, "Writer lock must be exclusive.");
	}
}