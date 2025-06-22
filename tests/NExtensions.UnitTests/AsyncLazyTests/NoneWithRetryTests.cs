using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class NoneWithRetryTests : NonParallelTests
{
	private const LazyAsyncThreadSafetyMode Mode = LazyAsyncThreadSafetyMode.NoneWithRetry;

	[Fact]
	public async Task GetNoneWithRetryAsync_CreatesOnce_OnSuccess()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), Mode);

		for (var i = 0; i < 3; i++)
		{
			_ = await asyncLazy;
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		VoidResult.GetCounter().ShouldBe(1);
		asyncLazy.HasFactory.ShouldBeFalse();
	}

	[Fact]
	public async Task GetNoneWithRetryAsync_Retries_OnError()
	{
		const int attempts = 3;
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), Mode);

		for (var i = 0; i < attempts; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Fact]
	public async Task GetNoneWithRetryAsync_Retries_OnFactoryError()
	{
		const int attempts = 3;
		var asyncLazy = new AsyncLazy<VoidResult>(_ => CtorException.ThrowsDirectly(), Mode);

		for (var i = 0; i < attempts; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Fact]
	public async Task GetNoneWithRetryAsync_ResetOnErrorEvenIfNotAwaited_OnError()
	{
		const int attempts = 3;
		const int sleep = 5;
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), Mode);

		for (var i = 0; i < attempts; i++)
		{
			var task = asyncLazy.GetValueAsync();
			task.IsCompleted.ShouldBeFalse();
			asyncLazy.IsValueCreated.ShouldBeFalse(); // Result cannot be there since it's an exception with retry policy.
			await Task.Delay(sleep * 10); // Some breathing room to ensure it completes.
			task.IsCompleted.ShouldBeTrue();
			task.IsFaulted.ShouldBeTrue();
			asyncLazy.IsValueCreated.ShouldBeFalse(); // Still the same.
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Fact]
	public void GetNoneWithRetryAsync_ResetOnErrorEvenIfNotAwaited_OnFactoryError()
	{
		const int attempts = 3;
		var asyncLazy = new AsyncLazy<VoidResult>(_ => CtorException.ThrowsDirectly(), Mode);

		for (var i = 0; i < attempts; i++)
		{
			var task = asyncLazy.GetValueAsync();
			task.IsCompleted.ShouldBeTrue();
			task.IsFaulted.ShouldBeTrue();
			asyncLazy.IsValueCreated.ShouldBeFalse();
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}
}