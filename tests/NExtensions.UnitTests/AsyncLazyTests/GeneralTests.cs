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

			VoidResult.Counter.ShouldBe(1);
		}
	}

	[Fact]
	public void AsyncLazy_IsRetryable_ForRetryablePolicies()
	{
		var asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyAsyncThreadSafetyMode.ExecutionAndPublication);
		asyncLazyNoRetry.IsRetryable.ShouldBeFalse();
		asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyAsyncThreadSafetyMode.None);
		asyncLazyNoRetry.IsRetryable.ShouldBeFalse();
		asyncLazyNoRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyAsyncThreadSafetyMode.PublicationOnly);
		asyncLazyNoRetry.IsRetryable.ShouldBeFalse();

		var asyncLazyWithRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyAsyncThreadSafetyMode.NoneWithRetry);
		asyncLazyWithRetry.IsRetryable.ShouldBeTrue();
		asyncLazyWithRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyAsyncThreadSafetyMode.PublicationOnlyWithRetry);
		asyncLazyWithRetry.IsRetryable.ShouldBeTrue();
		asyncLazyWithRetry = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyAsyncThreadSafetyMode.ExecutionAndPublicationWithRetry);
		asyncLazyWithRetry.IsRetryable.ShouldBeTrue();
	}
}