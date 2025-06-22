using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using Shouldly;

// ReSharper disable RedundantArgumentDefaultValue

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class GeneralTests : NonParallelTests
{
	[Fact]
	public async Task GetValueAsync_ReturnsTheSameInstance_AsIfDirectlyAwaited()
	{
		foreach (var mode in Enum.GetValues<LazyAsyncThreadSafetyMode>())
		{
			VoidResult.Reset();
			var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), mode);

			var value = await asyncLazy;
			var alsoValue = await asyncLazy.GetValueAsync();
			value.ShouldBe(alsoValue);

			VoidResult.GetCounter().ShouldBe(1);
			asyncLazy.IsValueCreated.ShouldBeTrue();
			asyncLazy.IsFaulted.ShouldBeFalse();
			asyncLazy.IsCompleted.ShouldBeTrue();
			asyncLazy.IsCompletedSuccessfully.ShouldBeTrue();
			asyncLazy.IsCanceled.ShouldBeFalse();
		}
	}

	[Fact]
	public async Task AsyncLazy_MaintainsProperState_OnSuccess()
	{
		var noRetryModes = new[] { LazyAsyncThreadSafetyMode.ExecutionAndPublication, LazyAsyncThreadSafetyMode.None, LazyAsyncThreadSafetyMode.PublicationOnly };
		foreach (var noRetryMode in noRetryModes)
		{
			VoidResult.Reset();
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), noRetryMode);
			await asyncLazyNoRetry;
			asyncLazyNoRetry.IsRetryable.ShouldBeFalse();
			asyncLazyNoRetry.IsValueCreated.ShouldBeTrue();
			asyncLazyNoRetry.IsFaulted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompleted.ShouldBeTrue();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeTrue();
			asyncLazyNoRetry.IsCanceled.ShouldBeFalse();
			VoidResult.GetCounter().ShouldBe(1);
		}

		var retryModes = new[]
			{ LazyAsyncThreadSafetyMode.NoneWithRetry, LazyAsyncThreadSafetyMode.PublicationOnlyWithRetry, LazyAsyncThreadSafetyMode.ExecutionAndPublicationWithRetry };
		foreach (var retryMode in retryModes)
		{
			VoidResult.Reset();
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), retryMode);
			asyncLazyNoRetry.IsRetryable.ShouldBeTrue();
			await asyncLazyNoRetry;
			asyncLazyNoRetry.IsValueCreated.ShouldBeTrue();
			asyncLazyNoRetry.IsFaulted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompleted.ShouldBeTrue();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeTrue();
			asyncLazyNoRetry.IsCanceled.ShouldBeFalse();
			VoidResult.GetCounter().ShouldBe(1);
		}
	}

	[Fact]
	public async Task AsyncLazy_MaintainsProperState_OnError()
	{
		var noRetryModes = new[] { LazyAsyncThreadSafetyMode.ExecutionAndPublication, LazyAsyncThreadSafetyMode.None, LazyAsyncThreadSafetyMode.PublicationOnly };
		foreach (var noRetryMode in noRetryModes)
		{
			CtorException.Reset();
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), noRetryMode);
			await Should.ThrowAsync<CtorException>(async () => await asyncLazyNoRetry);
			asyncLazyNoRetry.IsRetryable.ShouldBeFalse();
			asyncLazyNoRetry.IsValueCreated.ShouldBeTrue();
			asyncLazyNoRetry.IsFaulted.ShouldBeTrue();
			asyncLazyNoRetry.IsCompleted.ShouldBeTrue();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyNoRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(1);
		}

		var retryModes = new[]
			{ LazyAsyncThreadSafetyMode.NoneWithRetry, LazyAsyncThreadSafetyMode.PublicationOnlyWithRetry, LazyAsyncThreadSafetyMode.ExecutionAndPublicationWithRetry };
		foreach (var retryMode in retryModes)
		{
			CtorException.Reset();
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), retryMode);
			asyncLazyNoRetry.IsRetryable.ShouldBeTrue();
			await Should.ThrowAsync<CtorException>(async () => await asyncLazyNoRetry);
			// Carry little values in retryable mode
			asyncLazyNoRetry.IsValueCreated.ShouldBeFalse();
			asyncLazyNoRetry.IsFaulted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompleted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyNoRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(1);
		}
	}

	[Fact]
	public async Task AsyncLazy_MaintainsProperState_OnCanceledToken()
	{
		var noRetryModes = new[] { LazyAsyncThreadSafetyMode.ExecutionAndPublication, LazyAsyncThreadSafetyMode.None, LazyAsyncThreadSafetyMode.PublicationOnly };
		var cancelledToken = new CancellationToken(true);
		foreach (var noRetryMode in noRetryModes)
		{
			CtorException.Reset();
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(50, token), noRetryMode);
			await Should.ThrowAsync<OperationCanceledException>(async () => await asyncLazyNoRetry.GetValueAsync(cancelledToken));
			asyncLazyNoRetry.IsRetryable.ShouldBeFalse();
			asyncLazyNoRetry.IsValueCreated.ShouldBeFalse(); // Because it's an already cancelled token.
			asyncLazyNoRetry.IsFaulted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompleted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyNoRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(0);
		}

		var retryModes = new[]
			{ LazyAsyncThreadSafetyMode.NoneWithRetry, LazyAsyncThreadSafetyMode.PublicationOnlyWithRetry, LazyAsyncThreadSafetyMode.ExecutionAndPublicationWithRetry };
		foreach (var retryMode in retryModes)
		{
			CtorException.Reset();
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), retryMode);
			asyncLazyNoRetry.IsRetryable.ShouldBeTrue();
			await Should.ThrowAsync<OperationCanceledException>(async () => await asyncLazyNoRetry.GetValueAsync(cancelledToken));
			asyncLazyNoRetry.IsValueCreated.ShouldBeFalse();
			asyncLazyNoRetry.IsFaulted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompleted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyNoRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(0);
		}
	}

	[Fact]
	public async Task AsyncLazy_MaintainsProperState_OnCanceledTokenMidFlight()
	{
		const int sleep = 50;
		const int cancelAfter = 20;

		var noRetryModes = new[] { LazyAsyncThreadSafetyMode.ExecutionAndPublication, LazyAsyncThreadSafetyMode.None, LazyAsyncThreadSafetyMode.PublicationOnly };

		foreach (var noRetryMode in noRetryModes)
		{
			CtorException.Reset();
			var canceller = new CancellationTokenSource(cancelAfter);
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), noRetryMode);
			await Should.ThrowAsync<OperationCanceledException>(async () => await asyncLazyNoRetry.GetValueAsync(canceller.Token));
			asyncLazyNoRetry.IsRetryable.ShouldBeFalse();
			asyncLazyNoRetry.IsValueCreated.ShouldBeTrue();
			asyncLazyNoRetry.IsFaulted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompleted.ShouldBeTrue();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyNoRetry.IsCanceled.ShouldBeTrue();
			CtorException.GetCounter().ShouldBe(0);
			canceller.Dispose();
		}

		var retryModes = new[]
			{ LazyAsyncThreadSafetyMode.NoneWithRetry, LazyAsyncThreadSafetyMode.PublicationOnlyWithRetry, LazyAsyncThreadSafetyMode.ExecutionAndPublicationWithRetry };
		foreach (var retryMode in retryModes)
		{
			CtorException.Reset();
			var canceller = new CancellationTokenSource(cancelAfter);
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), retryMode);
			asyncLazyNoRetry.IsRetryable.ShouldBeTrue();
			await Should.ThrowAsync<OperationCanceledException>(async () => await asyncLazyNoRetry.GetValueAsync(canceller.Token));
			asyncLazyNoRetry.IsValueCreated.ShouldBeFalse();
			asyncLazyNoRetry.IsFaulted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompleted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyNoRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(0);
			canceller.Dispose();
		}
	}
}