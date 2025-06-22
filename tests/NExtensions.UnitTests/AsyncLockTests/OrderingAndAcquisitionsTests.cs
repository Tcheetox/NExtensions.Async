using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLockTests;

public class OrderingAndAcquisitionsTests
{
	[Fact]
	public async Task EnterScopeAsync_Release_WhenDisposed()
	{
		var sync = new AsyncLock();

		using (await sync.EnterScopeAsync())
		{
			await Task.Delay(1);
		}

		var acquiredSync = sync.EnterScopeAsync();
		acquiredSync.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Fact]
	public void EnterScopeAsync_EntersSynchronously_WhenPossible()
	{
		var sync = new AsyncLock();

		var enterTask = sync.EnterScopeAsync();
		enterTask.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Fact]
	public async Task EnterScopeAsync_WhenAwaitedSequentially_GrantsAccessInOrder()
	{
		var asyncLock = new AsyncLock();
		var accessLog = new List<int>();

		await Task.WhenAll(
			EnterAsync(1),
			EnterAsync(2),
			EnterAsync(3)
		);

		accessLog.Count.ShouldBe(3);
		accessLog.ShouldBe([1, 2, 3], false);
		return;

		async Task EnterAsync(int id)
		{
			using var scope = await asyncLock.EnterScopeAsync();
			accessLog.Add(id);
			await Task.Delay(10);
		}
	}

	[Fact]
	public async Task EnterScopeAsync_WhenMultipleWaiters_GrantsAccessOneAtATime()
	{
		var asyncLock = new AsyncLock();
		var concurrentAccesses = 0;
		var maxConcurrentAccesses = 0;

		var tasks = new[]
		{
			AccessAsync(),
			AccessAsync(),
			AccessAsync(),
			AccessAsync()
		};

		await Task.WhenAll(tasks);
		maxConcurrentAccesses.ShouldBe(1);

		return;

		async Task AccessAsync()
		{
			await Task.Yield();
			using var scope = await asyncLock.EnterScopeAsync();
			concurrentAccesses++;
			maxConcurrentAccesses = Math.Max(maxConcurrentAccesses, concurrentAccesses);
			await Task.Delay(10);
			concurrentAccesses--;
		}
	}

	[Fact]
	public async Task EnterScopeAsync_WhenFirstScopeDisposed_ReleaseNextWaiter()
	{
		var asyncLock = new AsyncLock();

		var firstScope = asyncLock.EnterScopeAsync();
		var secondScope = asyncLock.EnterScopeAsync();

		firstScope.IsCompletedSuccessfully.ShouldBeTrue();
		secondScope.IsCompleted.ShouldBeFalse();
		(await firstScope).Dispose();
		secondScope.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Fact]
	public async Task EnterScopeAsync_WhenCancelledBeforeEntry_ThrowsTaskCanceledException()
	{
		var asyncLock = new AsyncLock();
		var token = new CancellationToken(true);

		await Should.ThrowAsync<OperationCanceledException>(async () => { await asyncLock.EnterScopeAsync(token); });
	}

	[Fact]
	public async Task EnterScopeAsync_WhenCancelledWhileWaiting_ThrowsTaskCanceledException()
	{
		var asyncLock = new AsyncLock();

		var scope = await asyncLock.EnterScopeAsync();
		using var cts = new CancellationTokenSource();
		var waitingTask = asyncLock.EnterScopeAsync(cts.Token);

		await cts.CancelAsync();
		await Should.ThrowAsync<TaskCanceledException>(async () => await waitingTask);
		scope.Dispose();
	}

	[Fact]
	public async Task EnterScopeAsync_WhenCancelledWhileWaiting_ReleasesNextWaiter()
	{
		var asyncLock = new AsyncLock();

		var firstScope = await asyncLock.EnterScopeAsync();
		using var cts = new CancellationTokenSource();
		var cancelledWaiter = asyncLock.EnterScopeAsync(cts.Token);
		var nextWaiter = asyncLock.EnterScopeAsync(CancellationToken.None);
		nextWaiter.IsCompleted.ShouldBeFalse();
		await cts.CancelAsync();
		firstScope.Dispose();

		nextWaiter.IsCompletedSuccessfully.ShouldBeTrue();
		await Should.ThrowAsync<TaskCanceledException>(async () => await cancelledWaiter);
	}

	[Fact]
	public async Task EnterScopeAsync_WhenDisposedTwice_ThrowsObjectDisposedException()
	{
		var asyncLock = new AsyncLock();

		var scope = await asyncLock.EnterScopeAsync();
		scope.Dispose();

		Should.Throw<ObjectDisposedException>(() => scope.Dispose());
	}
}