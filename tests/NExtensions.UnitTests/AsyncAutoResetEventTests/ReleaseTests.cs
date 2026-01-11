using System.Collections.Concurrent;
using NExtensions.Async;
using NExtensions.UnitTests.Utilities;
using Shouldly;

// ReSharper disable AccessToModifiedClosure
// ReSharper disable AccessToDisposedClosure

namespace NExtensions.UnitTests.AsyncAutoResetEventTests;

public class ReleaseTests
{
	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task Set_ReleaseSingleWaiter_WhenOneThreadIsWaiting(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(false, syncContinuations);
		var wasReleased = false;
		var task = Task.Run(async () =>
		{
			await are.WaitAsync();
			wasReleased = true;
		});

		// Act
		are.Set();
		await task;

		// Assert
		wasReleased.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task Set_ShouldNotStoreSignals_MoreThanOnce(bool syncContinuations)
	{
		using var resetEvent = new AsyncAutoResetEvent(true, syncContinuations);
		resetEvent.Set();
		resetEvent.Set();
		var releasedCount = 0;

		var t1Got = false;
		var t2Got = false;
		var t1 = Task.Run(async () =>
		{
			await resetEvent.WaitAsync();
			t1Got = true;
			Interlocked.Increment(ref releasedCount);
		});
		var t2 = Task.Run(async () =>
		{
			await resetEvent.WaitAsync();
			t2Got = true;
			Interlocked.Increment(ref releasedCount);
		});

		await Task.WhenAny(t1, t2);
		await Task.Delay(30);
		releasedCount.ShouldBe(1);

		if (t1.IsCompleted)
		{
			t1Got.ShouldBeTrue();
			t2Got.ShouldBeFalse();
			t2.IsCompleted.ShouldBeFalse();
		}
		else
		{
			t1Got.ShouldBeFalse();
			t2Got.ShouldBeTrue();
			t2.IsCompleted.ShouldBeTrue();
		}
	}

	[Theory]
	[InlineData(30, 10, false)]
	[InlineData(50, 50, false)]
	[InlineData(30, 10, true)]
	[InlineData(50, 50, true)]
	public async Task Set_ShouldReleaseOneWaitingThreadPerSet_EvenInParallelNTasks(int waiters, int expectedReleases, bool syncContinuations)
	{
		using var resetEvent = new AsyncAutoResetEvent(false, syncContinuations);
		var releasedCount = 0;

		_ = Enumerable.Range(0, waiters)
			.Select(_ => Task.Run(async () =>
			{
				await resetEvent.WaitAsync();
				Interlocked.Increment(ref releasedCount);
			}))
			.ToArray();

		await Task.Delay(50);
		Parallel.For(0, expectedReleases, _ => resetEvent.Set());
		await Task.Delay(50);

		releasedCount.ShouldBe(expectedReleases);
	}

	[Theory]
	[InlineData(1, false)]
	[InlineData(16, false)]
	[InlineData(1, true)]
	[InlineData(16, true)]
	public async Task Set_ShouldReleaseOneWaitingThreadPerSet_EvenInParallelUnlessCancelled(int setParallelism, bool syncContinuations)
	{
		using var resetEvent = new AsyncAutoResetEvent(false, syncContinuations);
		using var cts = new CancellationTokenSource(10000);
		var random = new Random();
		const int maxSleepMs = 15;

		var acquiredCount = 0;
		var waitTask = ParallelUtility.ForAsync(0, 16, async (_, _) =>
		{
			while (!cts.IsCancellationRequested)
			{
				var sleep = random.Next(0, maxSleepMs);
				using var ownCts = new CancellationTokenSource(sleep);
				using var combined = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ownCts.Token);
				try
				{
					await resetEvent.WaitAsync(combined.Token);
					Interlocked.Increment(ref acquiredCount);
				}
				catch (OperationCanceledException)
				{
					// Noop.
				}
			}
		});

		var setCount = 0;
		var setTask = ParallelUtility.ForAsync(0, setParallelism, async (_, _) =>
		{
			while (!cts.IsCancellationRequested)
			{
				resetEvent.Set();
				Interlocked.Increment(ref setCount);
				var sleep = random.Next(-1, maxSleepMs);
				switch (sleep)
				{
					case > 0:
						await Task.Delay(sleep, CancellationToken.None);
						break;
					case 0:
						await Task.Yield();
						break;
				}
			}
		});

		await Task.WhenAll(setTask, waitTask);
		acquiredCount.ShouldBeLessThanOrEqualTo(setCount);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(16)]
	public async Task Set_ShouldReleaseOneWaitingThreadPerSet_EvenInParallelUnlessCancelled_Comparison(int setParallelism)
	{
		using var resetEvent = new AutoResetEvent(false);
		using var cts = new CancellationTokenSource(10000);
		var random = new Random();
		const int maxSleepMs = 15;

		var acquiredCount = 0;
		var waitTask = ParallelUtility.ForAsync(0, 16, (_, _) =>
		{
			while (!cts.IsCancellationRequested)
			{
				var sleep = random.Next(0, maxSleepMs);
				if (resetEvent.WaitOne(sleep))
					Interlocked.Increment(ref acquiredCount);
			}

			return ValueTask.CompletedTask;
		});

		var setCount = 0;
		var setTask = ParallelUtility.ForAsync(0, setParallelism, async (_, _) =>
		{
			while (!cts.IsCancellationRequested)
			{
				resetEvent.Set();
				Interlocked.Increment(ref setCount);
				var sleep = random.Next(-1, maxSleepMs);
				switch (sleep)
				{
					case > 0:
						await Task.Delay(sleep, CancellationToken.None);
						break;
					case 0:
						await Task.Yield();
						break;
				}
			}
		});

		await Task.WhenAll(setTask, waitTask);
		acquiredCount.ShouldBeLessThanOrEqualTo(setCount);
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task Set_ShouldReleaseOneWaitingThreadPerSet_EvenInParallel(bool syncContinuations)
	{
		using var resetEvent = new AsyncAutoResetEvent(false, syncContinuations);
		var releasedCount = 0;

		var t1 = Task.Run(async () =>
		{
			await resetEvent.WaitAsync();
			Interlocked.Increment(ref releasedCount);
		});
		var t2 = Task.Run(async () =>
		{
			await resetEvent.WaitAsync();
			Interlocked.Increment(ref releasedCount);
		});
		var t3 = Task.Run(async () =>
		{
			await resetEvent.WaitAsync();
			Interlocked.Increment(ref releasedCount);
		});

		await Task.Delay(50);
		Parallel.Invoke(
			() => resetEvent.Set(),
			() => resetEvent.Set(),
			() => resetEvent.Set()
		);

		await Task.WhenAll(t1, t2, t3);
		releasedCount.ShouldBe(3);
	}

	[Theory(Skip = "You can't assert for 'mostly'")]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task Set_ShouldReleasePendingThreads_MostlyInFifoOrder(bool syncContinuations)
	{
		// Arrange
		const int count = 50;
		using var autoResetEvent = new AsyncAutoResetEvent(false, syncContinuations);
		var releaseOrder = new ConcurrentQueue<int>();
		var allTasksWaiting = new TaskCompletionSource<bool>();
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
					allTasksWaiting.SetResult(true);
				}

				await autoResetEvent.WaitAsync();
				releaseOrder.Enqueue(taskId);
			});
			tasks.Add(task);
			await Task.Delay(10);
		}

		await allTasksWaiting.Task;
		await Task.Delay(50);

		// Release tasks one by one
		for (var i = 0; i < count; i++)
		{
			autoResetEvent.Set();
			await Task.Delay(10);
		}

		await Task.WhenAll(tasks);

		// Assert
		var actualOrder = releaseOrder.ToArray();
		actualOrder.ShouldBeInOrder();
		actualOrder.Length.ShouldBe(count);
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.Permutations), MemberType = typeof(AsyncAutoResetEventFactory))]
	public void Set_ThrowsObjectDisposedException_WhenCalledAfterDispose(bool initialState, bool syncContinuations)
	{
		var autoResetEvent = new AsyncAutoResetEvent(initialState, syncContinuations);

		autoResetEvent.Dispose();
		var act = () => autoResetEvent.Set();

		act.ShouldThrow<ObjectDisposedException>();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task Set_ReleaseOnlyOneWaiter_WhenMultipleTasksAreWaiting(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(false, syncContinuations);
		var releaseCount = 0;

		var t1 = Task.Run(async () =>
		{
			await are.WaitAsync();
			Interlocked.Increment(ref releaseCount);
		});
		var t2 = Task.Run(async () =>
		{
			await are.WaitAsync();
			Interlocked.Increment(ref releaseCount);
		});
		await Task.Delay(30);

		// Act
		are.Set();
		await Task.WhenAny(t1, t2);
		await Task.Delay(50);

		// Assert
		releaseCount.ShouldBe(1);
		are.Set();
		await Task.WhenAll(t1, t2);
		releaseCount.ShouldBe(2);
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public void Set_RemainSignaledUntilOneWait_WhenNoTaskIsWaiting(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(false, syncContinuations);

		// Act
		are.Set();

		// Assert
		var once = are.WaitAsync().AsTask();
		once.IsCompletedSuccessfully.ShouldBeTrue();
		var second = are.WaitAsync().AsTask();
		second.IsCompleted.ShouldBeFalse();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public void Set_BeIdempotentRegardingSignal_WhenCalledMultipleTimesWithoutWait(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(false, syncContinuations);

		// Act
		are.Set();
		are.Set();
		are.Set();

		// Assert
		are.WaitAsync().AsTask().IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public void Set_BeIdempotentRegardingSignal_WhenCalledInParallel(bool syncContinuations)
	{
		// Arrange
		const int parallelism = 10;
		const int hits = 1000;
		using var are = new AsyncAutoResetEvent(false, syncContinuations);

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

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task Set_ReleaseAllWaitingTasks_WhenCalledInParallel(bool syncContinuations)
	{
		// Arrange
		const int n = 50;
		using var are = new AsyncAutoResetEvent(false, syncContinuations);
		var count = 0;
		var tasks = new Task[n];
		for (var i = 0; i < n; i++)
		{
			tasks[i] = Task.Run(async () =>
			{
				await are.WaitAsync();
				Interlocked.Increment(ref count);
			});
		}

		await Task.Delay(50);

		// Act
		Parallel.For(0, n, _ => are.Set());

		await Task.WhenAll(tasks);

		// Assert
		count.ShouldBe(n);
	}
}