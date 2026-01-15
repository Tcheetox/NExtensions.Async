using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class NoneWithRetryTests : NonParallelTests
{
	private const LazyAsyncSafetyMode Mode = LazyAsyncSafetyMode.NoneWithRetry;

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetNoneWithRetry_CreatesOnce_OnSuccess(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => VoidResult.GetAsync(5, token), withCancellation, Mode);

		for (var i = 0; i < 3; i++)
		{
			_ = await asyncLazy;
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		VoidResult.GetCounter().ShouldBe(1);
		asyncLazy.HasFactory.ShouldBeFalse();
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetNoneWithRetry_Retries_OnError(bool withCancellation)
	{
		const int attempts = 3;
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(5, token), withCancellation, Mode);

		for (var i = 0; i < attempts; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetNoneWithRetry_Retries_OnFactoryError(bool withCancellation)
	{
		const int attempts = 3;
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(_ => CtorException.ThrowsDirectly(), withCancellation, Mode);

		for (var i = 0; i < attempts; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetNoneWithRetry_ResetOnErrorEvenIfNotAwaited_OnError(bool withCancellation)
	{
		const int attempts = 3;
		const int sleep = 5;
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), withCancellation, Mode);

		for (var i = 0; i < attempts; i++)
		{
			var task = asyncLazy.GetAsync();
			task.IsCompleted.ShouldBeFalse();
			asyncLazy.IsValueCreated.ShouldBeFalse(); // Results cannot be there since it's an exception with retry policy.
			await Task.Delay(sleep * 10); // Some breathing room to ensure it completes.
			task.IsCompleted.ShouldBeTrue();
			task.IsFaulted.ShouldBeTrue();
			asyncLazy.IsValueCreated.ShouldBeFalse(); // Still the same.
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public void GetNoneWithRetry_ResetOnErrorEvenIfNotAwaited_OnFactoryError(bool withCancellation)
	{
		const int attempts = 3;
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(_ => CtorException.ThrowsDirectly(), withCancellation, Mode);

		for (var i = 0; i < attempts; i++)
		{
			var task = asyncLazy.GetAsync();
			task.IsCompleted.ShouldBeTrue();
			task.IsFaulted.ShouldBeTrue();
			asyncLazy.IsValueCreated.ShouldBeFalse();
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}
}