namespace NExtensions.UnitTests.AsyncLockTests;

public class OrderingAndAcquisitionsTests
{
	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task EnterScopeAsync_Release_WhenDisposed(bool syncContinuation)
	{
		var sync = AsyncLockFactory.Create(syncContinuation);

		using (await sync.EnterScopeAsync())
		{
			await ValueTask.CompletedTask;
		}

		var acquiredSync = sync.EnterScopeAsync();
		acquiredSync.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public void EnterScopeAsync_EntersSynchronously_WhenPossible(bool syncContinuation)
	{
		var sync = AsyncLockFactory.Create(syncContinuation);

		var enterTask = sync.EnterScopeAsync();
		enterTask.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task EnterScopeAsync_WhenAwaitedSequentially_GrantsAccessInOrder(bool syncContinuation)
	{
		var asyncLock = AsyncLockFactory.Create(syncContinuation);
		var accessLog = new List<int>();

		await Task.WhenAll(
			EnterAsync(1),
			EnterAsync(2),
			EnterAsync(3)
		);

		var expected = new[] { 1, 2, 3 };
		accessLog.Count.ShouldBe(3);
		accessLog.ShouldBe(expected);
		return;

		async Task EnterAsync(int id)
		{
			using var scope = await asyncLock.EnterScopeAsync();
			accessLog.Add(id);
			await Task.Delay(5);
		}
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task EnterScopeAsync_WhenMultipleWaiters_GrantsAccessOneAtATime(bool syncContinuation)
	{
		var asyncLock = AsyncLockFactory.Create(syncContinuation);
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
			await Task.Delay(5);
			concurrentAccesses--;
		}
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task EnterScopeAsync_WhenFirstScopeDisposed_ReleaseNextWaiter(bool syncContinuation)
	{
		var asyncLock = AsyncLockFactory.Create(syncContinuation);

		var firstScope = asyncLock.EnterScopeAsync();
		var secondScope = asyncLock.EnterScopeAsync();

		firstScope.IsCompletedSuccessfully.ShouldBeTrue();
		secondScope.IsCompleted.ShouldBeFalse();
		(await firstScope).Dispose();
		secondScope.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task EnterScopeAsync_WhenCancelledBeforeEntry_ThrowsOperationCanceledException(bool syncContinuation)
	{
		var asyncLock = AsyncLockFactory.Create(syncContinuation);
		var token = new CancellationToken(true);

		await Should.ThrowAsync<OperationCanceledException>(async () => { await asyncLock.EnterScopeAsync(token); });
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task EnterScopeAsync_WhenCancelledWhileWaiting_ThrowsOperationCanceledException(bool syncContinuation)
	{
		var asyncLock = AsyncLockFactory.Create(syncContinuation);

		var scope = await asyncLock.EnterScopeAsync();
		using var cts = new CancellationTokenSource();
		var waitingTask = asyncLock.EnterScopeAsync(cts.Token);

		cts.Cancel();
		await Should.ThrowAsync<OperationCanceledException>(async () => await waitingTask);
		scope.Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task EnterScopeAsync_WhenCancelledWhileWaiting_ReleasesNextWaiter(bool syncContinuation)
	{
		var asyncLock = AsyncLockFactory.Create(syncContinuation);

		var firstScope = await asyncLock.EnterScopeAsync();
		using var cts = new CancellationTokenSource();
		var cancelledWaiter = asyncLock.EnterScopeAsync(cts.Token);
		var nextWaiter = asyncLock.EnterScopeAsync(CancellationToken.None);
		nextWaiter.IsCompleted.ShouldBeFalse();
		cts.Cancel();
		firstScope.Dispose();

		nextWaiter.IsCompletedSuccessfully.ShouldBeTrue();
		await Should.ThrowAsync<OperationCanceledException>(async () => await cancelledWaiter);
	}

	[Theory]
	[MemberData(nameof(AsyncLockFactory.ContinuationOptions), MemberType = typeof(AsyncLockFactory))]
	public async Task EnterScopeAsync_WhenDisposedTwice_ThrowsObjectDisposedException(bool syncContinuation)
	{
		var asyncLock = AsyncLockFactory.Create(syncContinuation);

		var scope = await asyncLock.EnterScopeAsync();
		scope.Dispose();

		Should.Throw<ObjectDisposedException>(() => scope.Dispose());
	}
}