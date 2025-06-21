using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using Shouldly;

// ReSharper disable RedundantArgumentDefaultValue

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class ThreadSafetyNoneTests : NonParallelTests
{
	[Fact]
	public async Task LazyAsync_CreatesOneInstance_WithNoRetryPolicy()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), LazyThreadSafetyMode.None, LazyRetryPolicy.None);

		for (var i = 0; i < 3; i++)
			_ = await asyncLazy;

		VoidResult.Counter.ShouldBe(1);
	}

	[Fact]
	public async Task LazyAsync_CreatesOneInstanceEvenOnFailures_WithNoRetryPolicy()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), LazyThreadSafetyMode.None, LazyRetryPolicy.None);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy.GetValueAsync());
		}

		CtorException.Counter.ShouldBe(1);
	}

	[Fact]
	public async Task LazyAsync_RetryOnFailures_WithRetryPolicy()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), LazyThreadSafetyMode.None, LazyRetryPolicy.Retry);

		const int attempts = 3;
		for (var i = 0; i < attempts; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
		}

		CtorException.Counter.ShouldBe(attempts);
	}

	[Fact]
	public async Task LazyAsync_ResetOnFailure_EvenIfNotAwaitedWithRetryPolicy()
	{
		const int sleep = 3;
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), LazyThreadSafetyMode.None, LazyRetryPolicy.Retry);

		var task = asyncLazy.GetValueAsync();
		task.IsCompleted.ShouldBeFalse();
		asyncLazy.IsValueCreated.ShouldBeTrue(); // Even if not awaited, the result is gettin' there.
		await Task.Delay(sleep * 10); // Some breathing room to ensure it completes.
		task.IsCompleted.ShouldBeTrue();
		task.IsFaulted.ShouldBeTrue();
		asyncLazy.IsValueCreated.ShouldBeFalse(); // No longer true.
		CtorException.Counter.ShouldBe(1);
	}
}