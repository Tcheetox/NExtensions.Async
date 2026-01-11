using System.Collections.Concurrent;
using NExtensions.Async;
using NExtensions.UnitTests.Utilities;
using Shouldly;

// ReSharper disable AccessToModifiedClosure
// ReSharper disable AccessToDisposedClosure

namespace NExtensions.UnitTests.AsyncManualResetEventTests;

public class ReleaseTests
{
	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task Set_ReleaseSingleWaiter_WhenOneThreadIsWaiting(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		var wasReleased = false;
		var task = Task.Run(async () =>
		{
			await mre.WaitAsync();
			wasReleased = true;
		});

		// Act
		mre.Set();
		await task;

		// Assert
		wasReleased.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task Set_ShouldReleaseAllWaiters_WhenCalled(bool syncContinuations)
	{
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		var releasedCount = 0;
		const int taskCount = 10;
		var tasks = new Task[taskCount];

		for (var i = 0; i < taskCount; i++)
		{
			tasks[i] = Task.Run(async () =>
			{
				await mre.WaitAsync();
				Interlocked.Increment(ref releasedCount);
			});
		}

		await Task.Delay(50);
		releasedCount.ShouldBe(0);

		// Act
		mre.Set();
		await Task.WhenAll(tasks);

		// Assert
		releasedCount.ShouldBe(taskCount);
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task Set_ShouldRemainSignaled_UntilReset(bool syncContinuations)
	{
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		mre.Set();

		var t1 = mre.WaitAsync().AsTask();
		var t2 = mre.WaitAsync().AsTask();

		await Task.WhenAll(t1, t2);

		t1.IsCompletedSuccessfully.ShouldBeTrue();
		t2.IsCompletedSuccessfully.ShouldBeTrue();

		mre.Reset();
		var t3 = mre.WaitAsync().AsTask();
		await Task.Delay(50);
		t3.IsCompleted.ShouldBeFalse();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task Set_ShouldReleasePendingThreads_MostlyInFifoOrder(bool syncContinuations)
	{
		// Arrange
		const int count = 50;
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		var releaseOrder = new ConcurrentQueue<int>();
		var allTasksWaiting = new TaskCompletionSource();
		var waitingCount = 0;
		var tasks = new List<Task>();

		// Act
		for (var i = 0; i < count; i++)
		{
			var taskId = i;
			var task = Task.Run(async () =>
			{
				if (Interlocked.Increment(ref waitingCount) == count)
				{
					allTasksWaiting.SetResult();
				}

				await mre.WaitAsync();
				releaseOrder.Enqueue(taskId);
			});
			tasks.Add(task);
			await Task.Delay(10);
		}

		await allTasksWaiting.Task;
		mre.Set();
		await Task.WhenAll(tasks);

		// Assert
		var actualOrder = releaseOrder.ToArray();
		actualOrder.Length.ShouldBe(count);
		if (syncContinuations)
		{
			actualOrder.ShouldBeInOrder();
		}
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.Permutations), MemberType = typeof(AsyncManualResetEventFactory))]
	public void Set_ThrowsObjectDisposedException_WhenCalledAfterDispose(bool initialState, bool syncContinuations)
	{
		var mre = new AsyncManualResetEvent(initialState, syncContinuations);

		mre.Dispose();
		var act = () => mre.Set();

		act.ShouldThrow<ObjectDisposedException>();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public void Set_BeIdempotentRegardingSignal_WhenCalledMultipleTimesWithoutWait(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(false, syncContinuations);

		// Act
		mre.Set();
		mre.Set();
		mre.Set();

		// Assert
		mre.WaitAsync().AsTask().IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task Set_ReleaseAllWaitingTasks_WhenCalledInParallel(bool syncContinuations)
	{
		// Arrange
		const int n = 50;
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		var count = 0;
		var tasks = new Task[n];
		var tcs = new TaskCompletionSource();

		for (var i = 0; i < n; i++)
		{
			var taskId = i;
			tasks[i] = Task.Run(async () =>
			{
				if (taskId == n - 1)
					tcs.SetResult();
				await mre.WaitAsync();
				Interlocked.Increment(ref count);
			});
		}

		await tcs.Task;

		// Act
		Parallel.For(0, n, _ => mre.Set());

		await Task.WhenAll(tasks);

		// Assert
		count.ShouldBe(n);
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public void Set_BeIdempotentRegardingSignal_WhenCalledInParallel(bool syncContinuations)
	{
		// Arrange
		const int parallelism = 10;
		const int hits = 1000;
		using var are = new AsyncManualResetEvent(false, syncContinuations);

		// Act & Assert
		var waits = 0;
		var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
		var waitAll = ParallelUtility.ForAsync(0, parallelism, options, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > hits) return;
				await are.WaitAsync(CancellationToken.None);
			}
		});

		Parallel.For(0, parallelism, options, _ =>
		{
			while (!waitAll.IsCompletedSuccessfully)
			{
				var act = () => are.Set();
				act.ShouldNotThrow();
			}
		});
		waits.ShouldBeGreaterThanOrEqualTo(hits);
	}
}