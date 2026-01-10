using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncManualResetEventTests;

public class GeneralTests
{
	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public void Constructor_SetInitialStateToSignaled_WhenTrueIsPassed(bool syncContinuations)
	{
		// Arrange & Act
		using var mre = new AsyncManualResetEvent(true, syncContinuations);

		// Assert
		var task = mre.WaitAsync(CancellationToken.None);
		task.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task Constructor_SetInitialStateToSignaled_WhenFalseIsPassed(bool syncContinuations)
	{
		// Arrange & Act
		using var mre = new AsyncManualResetEvent(false, syncContinuations);

		// Assert
		var task = mre.WaitAsync(CancellationToken.None);
		await Task.Delay(50);
		task.IsCompletedSuccessfully.ShouldBeFalse();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.Permutations), MemberType = typeof(AsyncManualResetEventFactory))]
	public void Dispose_DoesNotThrow_ForMultipleCalls(bool initialState, bool syncContinuations)
	{
		var manualResetEvent = new AsyncManualResetEvent(initialState, syncContinuations);

		manualResetEvent.Dispose();
		var act = () => manualResetEvent.Dispose();

		act.ShouldNotThrow();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.Permutations), MemberType = typeof(AsyncManualResetEventFactory))]
	public void Reset_ThrowsObjectDisposedException_WhenCalledAfterDispose(bool initialState, bool syncContinuations)
	{
		var manualResetEvent = new AsyncManualResetEvent(initialState, syncContinuations);

		manualResetEvent.Dispose();
		var act = () => manualResetEvent.Reset();

		act.ShouldThrow<ObjectDisposedException>();
	}

	[Theory]
	[MemberData(nameof(AsyncManualResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncManualResetEventFactory))]
	public async Task Reset_ChangeStateToUnsignaled_WhenStateWasSignaled(bool syncContinuations)
	{
		// Arrange
		using var mre = new AsyncManualResetEvent(true, syncContinuations);

		// Act
		mre.Reset();

		// Assert
		mre.WaitAsync().AsTask().IsCompleted.ShouldBeFalse();
		using var cts = new CancellationTokenSource(10);
		await Should.ThrowAsync<OperationCanceledException>(async () => await mre.WaitAsync(cts.Token));
	}
}
