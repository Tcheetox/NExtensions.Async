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
		var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyThreadSafetyMode.None, LazyRetryPolicy.None);

		var value = await asyncLazy;
		var alsoValue = await asyncLazy.GetValueAsync();
		value.ShouldBe(alsoValue);

		VoidResult.Counter.ShouldBe(1);
	}

	[Fact]
	public void AsyncLazy_IsRetryable_ForRetryablePolicies()
	{
		var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyThreadSafetyMode.None, LazyRetryPolicy.None);
		asyncLazyNoRetry.IsRetryable.ShouldBeFalse();
		var asyncLazyWithRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyThreadSafetyMode.None, LazyRetryPolicy.Retry);
		asyncLazyWithRetry.IsRetryable.ShouldBeTrue();
		var asyncLazyWithStrictRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyThreadSafetyMode.None, LazyRetryPolicy.StrictRetry);
		asyncLazyWithStrictRetry.IsRetryable.ShouldBeTrue();
	}
}