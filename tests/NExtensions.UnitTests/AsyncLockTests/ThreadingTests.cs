using NExtensions.UnitTests.Utilities;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLockTests;

public class ThreadingTests
{
	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task AsyncLock_AllowsOnlySingleEntryConcurrently(bool syncContinuation)
	{
		var asyncLock = AsyncLockFactory.Create(syncContinuation);
		var concurrentCount = 0;
		var maxConcurrent = 0;

		var tasks = Enumerable.Range(0, 10).Select(async _ =>
		{
			using (await asyncLock.EnterScopeAsync())
			{
				Interlocked.Increment(ref concurrentCount);
				maxConcurrent = Math.Max(maxConcurrent, concurrentCount);

				// Simulate work
				await Task.Delay(3);

				Interlocked.Decrement(ref concurrentCount);
			}
		}).ToArray();

		await Task.WhenAll(tasks);

		maxConcurrent.ShouldBe(1, "AsyncLock should allow only one concurrent entry.");
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task AsyncLock_AllowsOnlySingleEntryConcurrently_WithRandomCancellation(bool syncContinuation)
	{
		const int expectedHits = 10_000;
		var asyncLock = AsyncLockFactory.Create(syncContinuation);
		var concurrentCount = 0;
		var totalHits = 0;
		var failedHits = 0;

		await ParallelUtility.ForAsync(0, expectedHits, async (_, _) =>
		{
			try
			{
				var cancel = Random.Shared.Next(0, 5);
				using var cts = new CancellationTokenSource(cancel);

				using (await asyncLock.EnterScopeAsync(cts.Token))
				{
					var current = Interlocked.Increment(ref concurrentCount);
					current.ShouldBe(1);
					Interlocked.Increment(ref totalHits);
					await Task.Delay(1, CancellationToken.None);
					Interlocked.Decrement(ref concurrentCount);
				}
			}
			catch
			{
				// Yep.
				Interlocked.Increment(ref failedHits);
			}
		});
		
		(failedHits + totalHits).ShouldBe(expectedHits, "All attempts should be either successful or cancelled.");
		failedHits.ShouldBeGreaterThan(0);
	}
}