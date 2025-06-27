using NExtensions.Async;
using NExtensions.UnitTests.Utilities;
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
				await Task.Delay(3);

				Interlocked.Decrement(ref concurrentCount);
			}
		}).ToArray();

		await Task.WhenAll(tasks);

		maxConcurrent.ShouldBe(1, "AsyncLock should allow only one concurrent entry.");
	}
	
	[Fact]
	public async Task AsyncLock_CancelledWaiter_CannotBeTheOneActive()
	{
		// This test is not great, it requires a Thread.Sleep() right after _active = true in Release() to reproduce the correct bug.
		var asyncLock = new AsyncLock();
		var cts = new CancellationTokenSource();

		var releaser1 = await asyncLock.EnterScopeAsync(CancellationToken.None);
		var waiterTask = asyncLock.EnterScopeAsync(cts.Token).AsTask();
		var nextTaker = asyncLock.EnterScopeAsync(CancellationToken.None);

		var dispose = Task.Run(() => releaser1.Dispose(), CancellationToken.None);
		var cancel = Task.Run(() => cts.Cancel(), CancellationToken.None);

		await Task.WhenAll(cancel, dispose);
	
		if (waiterTask.IsCompletedSuccessfully)
		{
			await Should.NotThrowAsync(async () => (await waiterTask).Dispose());
		}
		else
		{
			waiterTask.IsCanceled.ShouldBeTrue();
		}

		nextTaker.IsCompletedSuccessfully.ShouldBeTrue();
		cts.Dispose();
	}
}