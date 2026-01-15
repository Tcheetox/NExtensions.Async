using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;

// ReSharper disable RedundantArgumentDefaultValue

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class GeneralTests : NonParallelTests
{
	[Fact]
	public async Task AsyncLazy_CtorOverloads_Matches()
	{
		var result = new object();
		var lazy = new AsyncLazy<object>(() => Task.FromResult(result));
		var lazyOverload = new AsyncLazy<object>(_ => Task.FromResult(result));

		var resultViaWaiter = await lazy;
		resultViaWaiter.ShouldBe(result);
		var resultViaGetValueAsync = await lazy.GetAsync();
		resultViaGetValueAsync.ShouldBe(result);
		lazy.IsRetryable.ShouldBe(lazyOverload.IsRetryable);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetValueAsync_ReturnsTheSameInstance_AsIfDirectlyAwaited(bool withCancellation)
	{
		foreach (var mode in Enum.GetValues<LazyAsyncSafetyMode>())
		{
			VoidResult.Reset();
			var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => VoidResult.GetAsync(5, token), withCancellation, mode);

			var value = await asyncLazy;
			var alsoValue = await asyncLazy.GetAsync();
			value.ShouldBe(alsoValue);

			VoidResult.GetCounter().ShouldBe(1);
			asyncLazy.IsValueCreated.ShouldBeTrue();
			asyncLazy.IsFaulted.ShouldBeFalse();
			asyncLazy.IsCompleted.ShouldBeTrue();
			asyncLazy.IsCompletedSuccessfully.ShouldBeTrue();
			asyncLazy.IsCanceled.ShouldBeFalse();
		}
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task AsyncLazy_MaintainsProperState_OnSuccess(bool withCancellation)
	{
		var noRetryModes = new[] { LazyAsyncSafetyMode.ExecutionAndPublication, LazyAsyncSafetyMode.None };
		foreach (var noRetryMode in noRetryModes)
		{
			VoidResult.Reset();
			var asyncLazyNoRetry = AsyncLazyFactory.Create<VoidResult>(token => VoidResult.GetAsync(5, token), withCancellation, noRetryMode);
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
			{ LazyAsyncSafetyMode.NoneWithRetry, LazyAsyncSafetyMode.PublicationOnly, LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry };
		foreach (var retryMode in retryModes)
		{
			VoidResult.Reset();
			var asyncLazyNoRetry = AsyncLazyFactory.Create<VoidResult>(token => VoidResult.GetAsync(5, token), withCancellation, retryMode);
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

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task AsyncLazy_MaintainsProperState_OnError(bool withCancellation)
	{
		var noRetryModes = new[] { LazyAsyncSafetyMode.ExecutionAndPublication, LazyAsyncSafetyMode.None };
		foreach (var noRetryMode in noRetryModes)
		{
			CtorException.Reset();
			var asyncLazyNoRetry = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(5, token), withCancellation, noRetryMode);
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
			{ LazyAsyncSafetyMode.NoneWithRetry, LazyAsyncSafetyMode.PublicationOnly, LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry };
		foreach (var retryMode in retryModes)
		{
			CtorException.Reset();
			var asyncLazyRetry = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(5, token), withCancellation, retryMode);
			asyncLazyRetry.IsRetryable.ShouldBeTrue();
			await Should.ThrowAsync<CtorException>(async () => await asyncLazyRetry);
			// Carry little values in retryable mode
			asyncLazyRetry.IsValueCreated.ShouldBeFalse();
			asyncLazyRetry.IsFaulted.ShouldBeFalse();
			asyncLazyRetry.IsCompleted.ShouldBeFalse();
			asyncLazyRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(1);
		}
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task AsyncLazy_MaintainsProperState_OnCanceledToken(bool withCancellation)
	{
		var noRetryModes = new[] { LazyAsyncSafetyMode.ExecutionAndPublication, LazyAsyncSafetyMode.None };
		var cancelledToken = new CancellationToken(true);
		foreach (var noRetryMode in noRetryModes)
		{
			CtorException.Reset();
			var asyncLazyNoRetry = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(50, token), withCancellation, noRetryMode);
			await Should.ThrowAsync<OperationCanceledException>(async () => await asyncLazyNoRetry.GetAsync(cancelledToken));
			asyncLazyNoRetry.IsRetryable.ShouldBeFalse();
			asyncLazyNoRetry.IsValueCreated.ShouldBeFalse(); // Because it's an already cancelled token.
			asyncLazyNoRetry.IsFaulted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompleted.ShouldBeFalse();
			asyncLazyNoRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyNoRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(0);
		}

		var retryModes = new[]
			{ LazyAsyncSafetyMode.NoneWithRetry, LazyAsyncSafetyMode.PublicationOnly, LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry };
		foreach (var retryMode in retryModes)
		{
			CtorException.Reset();
			var asyncLazyRetry = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(5, token), withCancellation, retryMode);
			asyncLazyRetry.IsRetryable.ShouldBeTrue();
			await Should.ThrowAsync<OperationCanceledException>(async () => await asyncLazyRetry.GetAsync(cancelledToken));
			asyncLazyRetry.IsValueCreated.ShouldBeFalse();
			asyncLazyRetry.IsFaulted.ShouldBeFalse();
			asyncLazyRetry.IsCompleted.ShouldBeFalse();
			asyncLazyRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(0);
		}
	}

	[Fact]
	public async Task AsyncLazy_MaintainsProperState_OnCanceledTokenMidFlight()
	{
		// Only valid when using the ctor supporting factory with cancellation tokens.
		const int sleep = 50;
		const int cancelAfter = 20;

		var noRetryModes = new[] { LazyAsyncSafetyMode.ExecutionAndPublication, LazyAsyncSafetyMode.None };

		foreach (var noRetryMode in noRetryModes)
		{
			CtorException.Reset();
			var canceller = new CancellationTokenSource(cancelAfter);
			var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), noRetryMode);
			await Should.ThrowAsync<OperationCanceledException>(async () => await asyncLazyNoRetry.GetAsync(canceller.Token));
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
			{ LazyAsyncSafetyMode.NoneWithRetry, LazyAsyncSafetyMode.PublicationOnly, LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry };
		foreach (var retryMode in retryModes)
		{
			CtorException.Reset();
			var canceller = new CancellationTokenSource(cancelAfter);
			var asyncLazyRetry = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), retryMode);
			asyncLazyRetry.IsRetryable.ShouldBeTrue();
			await Should.ThrowAsync<OperationCanceledException>(async () => await asyncLazyRetry.GetAsync(canceller.Token));
			asyncLazyRetry.IsValueCreated.ShouldBeFalse();
			asyncLazyRetry.IsFaulted.ShouldBeFalse();
			asyncLazyRetry.IsCompleted.ShouldBeFalse();
			asyncLazyRetry.IsCompletedSuccessfully.ShouldBeFalse();
			asyncLazyRetry.IsCanceled.ShouldBeFalse();
			CtorException.GetCounter().ShouldBe(0);
			canceller.Dispose();
		}
	}
}