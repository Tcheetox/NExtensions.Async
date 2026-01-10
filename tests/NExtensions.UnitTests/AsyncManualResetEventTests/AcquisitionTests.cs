using NExtensions.Async;
using Shouldly;

// ReSharper disable AccessToDisposedClosure

namespace NExtensions.UnitTests.AsyncManualResetEventTests;

public class AcquisitionTests
{
	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task WaitAsync_ThrowsCancelledOperationException_WhenCalledOnUnsignaledEventWithCancelledToken(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		var cancelledToken = new CancellationToken(true);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () => await mre.WaitAsync(cancelledToken));
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task WaitAsync_ThrowsCancelledOperationException_WhenCalledOnSignaledEventWithCancelledToken(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(true, syncContinuations);
		var cancelledToken = new CancellationToken(true);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () => await mre.WaitAsync(cancelledToken));
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task WaitAsync_ShouldBlockUntilSetOrTimeout_WhenCalledOnUnsignaledEvent(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		using var cts = new CancellationTokenSource(10);

		// Act
		var waitTask = mre.WaitAsync(cts.Token).AsTask();

		// Assert
		waitTask.IsCompleted.ShouldBeFalse();
		waitTask.IsCanceled.ShouldBeFalse();
		await Task.Delay(75, CancellationToken.None);
		waitTask.IsCompleted.ShouldBeTrue();
		waitTask.IsCanceled.ShouldBeTrue();

		await Should.ThrowAsync<OperationCanceledException>(async () => await waitTask);
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task WaitAsync_HangsUntilSet_WhenStateIsUnsignaled(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		var wasSignaled = false;
		var task = Task.Run(async () =>
		{
			await mre.WaitAsync();
			wasSignaled = true;
		});

		// Act
		await Task.Delay(50);
		wasSignaled.ShouldBeFalse();
		mre.Set();
		await task;

		// Assert
		wasSignaled.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task WaitAsync_ThrowOperationCanceledException_WhenStateIsUnsignaledAndTimeoutOccurs(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(false, syncContinuations);
		using var cts = new CancellationTokenSource(10);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () => await mre.WaitAsync(cts.Token));
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.Permutations), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task WaitAsync_ThrowsObjectDisposedException_WhenCalledAfterDispose(bool initialState, bool syncContinuations)
	{
		var manualResetEvent = new AsyncManualResetEvent(initialState, syncContinuations);

		manualResetEvent.Dispose();
		var act = async () => await manualResetEvent.WaitAsync(CancellationToken.None);

		await act.ShouldThrowAsync<ObjectDisposedException>();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public void WaitAsync_HangsForever_WhenDisposedDuringWait(bool syncContinuations)
	{
		var manualResetEvent = new AsyncManualResetEvent(false, syncContinuations);

		var infiniteWait = manualResetEvent.WaitAsync(CancellationToken.None).AsTask();
		manualResetEvent.Dispose();

		infiniteWait.IsCanceled.ShouldBeFalse();
		infiniteWait.IsFaulted.ShouldBeFalse();
		infiniteWait.IsCompleted.ShouldBeFalse();
		infiniteWait.Status.ShouldBe(TaskStatus.WaitingForActivation);
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task WaitAsync_ShouldReturnAll_WhenTasksWaitAsyncOnSignaledEvent(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(true, syncContinuations);
		var successCount = 0;
		var cancelledCount = 0;
		const int taskCount = 20;
		var tasks = new Task[taskCount];

		// Act
		for (var i = 0; i < taskCount; i++)
		{
			tasks[i] = Task.Run(async () =>
			{
				using var cts = new CancellationTokenSource(50);
				try
				{
					await mre.WaitAsync(cts.Token);
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
		successCount.ShouldBe(taskCount);
		cancelledCount.ShouldBe(0);
	}
}
