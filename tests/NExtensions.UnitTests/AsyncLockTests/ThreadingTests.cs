using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLockTests;

public class ThreadingTests
{
	[Fact]
	public async Task AsyncLock_AllowsOnlySingleEntryConcurrently()
	{
		var asyncLock = new AsyncLock();
		var concurrentCount = 0;
		var maxConcurrent = 0;

		var tasks = Enumerable.Range(0, 10).Select(async _ =>
		{
			using (await asyncLock.EnterScopeAsync())
			{
				Interlocked.Increment(ref concurrentCount);
				maxConcurrent = Math.Max(maxConcurrent, concurrentCount);

				// Simulate work
				await Task.Delay(10);

				Interlocked.Decrement(ref concurrentCount);
			}
		}).ToArray();

		await Task.WhenAll(tasks);

		maxConcurrent.ShouldBe(1, "AsyncLock should allow only one concurrent entry.");
	}
}