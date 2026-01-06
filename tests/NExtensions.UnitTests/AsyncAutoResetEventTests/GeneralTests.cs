using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncAutoResetEventTests;

public class GeneralTests
{
	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public void Constructor_SetInitialStateToSignaled_WhenTrueIsPassed(bool syncContinuations)
	{
		// Arrange & Act
		using var are = new AsyncAutoResetEvent(true, syncContinuations);

		// Assert
		var task = are.WaitAsync(CancellationToken.None);
		task.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task Constructor_SetInitialStateToSignaled_WhenFalseIsPassed(bool syncContinuations)
	{
		// Arrange & Act
		using var are = new AsyncAutoResetEvent(false, syncContinuations);

		// Assert
		var task = are.WaitAsync(CancellationToken.None);
		await Task.Delay(50);
		task.IsCompletedSuccessfully.ShouldBeFalse();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.Permutations), MemberType = typeof(AsyncAutoResetEventFactory))]
	public void Dispose_DoesNotThrow_ForMultipleCalls(bool initialState, bool syncContinuations)
	{
		var autoResetEvent = new AsyncAutoResetEvent(initialState, syncContinuations);

		autoResetEvent.Dispose();
		var act = () => autoResetEvent.Dispose();

		act.ShouldNotThrow();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.Permutations), MemberType = typeof(AsyncAutoResetEventFactory))]
	public void Reset_ThrowsObjectDisposedException_WhenCalledAfterDispose(bool initialState, bool syncContinuations)
	{
		var autoResetEvent = new AsyncAutoResetEvent(initialState, syncContinuations);

		autoResetEvent.Dispose();
		var act = () => autoResetEvent.Reset();

		act.ShouldThrow<ObjectDisposedException>();
	}

	[Theory]
	[MemberData(nameof(AsyncAutoResetEventFactory.ContinuationOptions), MemberType = typeof(AsyncAutoResetEventFactory))]
	public async Task Reset_ChangeStateToUnsignaled_WhenStateWasSignaled(bool syncContinuations)
	{
		// Arrange
		using var are = new AsyncAutoResetEvent(true, syncContinuations);

		// Act
		are.Reset();

		// Assert
		are.WaitAsync().AsTask().IsCompleted.ShouldBeFalse();
		using var cts = new CancellationTokenSource(10);
		await Should.ThrowAsync<OperationCanceledException>(async () => await are.WaitAsync(cts.Token));
	}
}