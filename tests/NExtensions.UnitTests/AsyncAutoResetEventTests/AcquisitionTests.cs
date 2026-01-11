using NExtensions.Async;
using Shouldly;

// ReSharper disable AccessToDisposedClosure

namespace NExtensions.UnitTests.AsyncAutoResetEventTests;

public class AcquisitionTests
{
	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task WaitAsync_ThrowsCancelledOperationException_WhenCalledOnUnsignaledEventWithCancelledToken(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(false, syncContinuations);
		var cancelledToken = new CancellationToken(true);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () => await are.WaitAsync(cancelledToken));
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task WaitAsync_ThrowsCancelledOperationException_WhenCalledOnSignaledEventWithCancelledToken(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(true, syncContinuations);
		var cancelledToken = new CancellationToken(true);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () => await are.WaitAsync(cancelledToken));
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task WaitAsync_ShouldBlockUntilSetOrTimeout_WhenCalledOnUnsignaledEvent(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(false, syncContinuations);
		using var cts = new CancellationTokenSource(10);
		var tcs = new TaskCompletionSource();

		// Act
		var waitTask = are
			.WaitAsync(cts.Token)
			.AsTask();
		_ = waitTask.ContinueWith(_ => tcs.SetResult(), TaskContinuationOptions.OnlyOnCanceled);

		// Assert
		waitTask.IsCompleted.ShouldBeFalse();
		waitTask.IsCanceled.ShouldBeFalse();
		await tcs.Task;
		waitTask.IsCompleted.ShouldBeTrue();
		waitTask.IsCanceled.ShouldBeTrue();

		await Should.ThrowAsync<OperationCanceledException>(async () => await waitTask);
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task WaitAsync_HangsUntilSet_WhenStateIsUnsignaled(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(false, syncContinuations);
		var wasSignaled = false;
		var task = Task.Run(async () =>
		{
			await are.WaitAsync();
			wasSignaled = true;
		});

		// Act
		await Task.Delay(30);
		wasSignaled.ShouldBeFalse();
		are.Set();
		await task;

		// Assert
		wasSignaled.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task WaitAsync_ThrowOperationCanceledException_WhenStateIsUnsignaledAndTimeoutOccurs(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(false, syncContinuations);
		using var cts = new CancellationTokenSource(10);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () => await are.WaitAsync(cts.Token));
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.Permutations), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task WaitAsync_ThrowsObjectDisposedException_WhenCalledAfterDispose(bool initialState, bool syncContinuations)
	{
		var autoResetEvent = new AsyncAutoResetEvent(initialState, syncContinuations);

		autoResetEvent.Dispose();
		var act = async () => await autoResetEvent.WaitAsync(CancellationToken.None);

		await act.ShouldThrowAsync<ObjectDisposedException>();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public void WaitAsync_HangsForever_WhenDisposedDuringWait(bool syncContinuations)
	{
		var autoResetEvent = new AsyncAutoResetEvent(false, syncContinuations);

		var infiniteWait = autoResetEvent.WaitAsync(CancellationToken.None).AsTask();
		autoResetEvent.Dispose();

		infiniteWait.IsCanceled.ShouldBeFalse();
		infiniteWait.IsFaulted.ShouldBeFalse();
		infiniteWait.IsCompleted.ShouldBeFalse();
		infiniteWait.Status.ShouldBe(TaskStatus.WaitingForActivation);
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task WaitAsync_ShouldReturnOnce_WhenTasksWaitAsyncOnSignaledEvent(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(true, syncContinuations);
		var successCount = 0;
		var cancelledCount = 0;
		const int taskCount = 20;
		var tasks = new Task[taskCount];

		// Act
		for (var i = 0; i < taskCount; i++)
		{
			tasks[i] = Task.Run(async () =>
			{
				using var cts = new CancellationTokenSource(25);
				try
				{
					await are.WaitAsync(cts.Token);
					Interlocked.Increment(ref successCount);
				}
				catch (OperationCanceledException)
				{
					Interlocked.Increment(ref cancelledCount);
				}
			});
		}

		await Task.WhenAll(tasks);

		// Assert
		successCount.ShouldBe(1);
		cancelledCount.ShouldBe(taskCount - 1);
	}
}