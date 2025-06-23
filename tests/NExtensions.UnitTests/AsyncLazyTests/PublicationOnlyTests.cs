using System.Collections.Concurrent;
using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class PublicationOnlyTests : NonParallelTests
{
	private const LazyAsyncThreadSafetyMode Mode = LazyAsyncThreadSafetyMode.PublicationOnly;

	[Fact]
	public async Task GetPublicationOnly_CreatesOnce_OnSuccess()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), Mode);

		for (var i = 0; i < 3; i++)
		{
			_ = await asyncLazy;
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		VoidResult.GetCounter().ShouldBe(1);
	}

	[Fact]
	public async Task GetPublicationOnly_Retries_OnError()
	{
		const int attempts = 3;
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Fact]
	public async Task GetPublicationOnly_Retries_OnFactoryError()
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
	public async Task GetPublicationOnly_PublishesOnce_OnSuccess()
	{
		const int attempts = 10;
		const int sleep = 30;
		var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(sleep, token), Mode);
		var bag = new ConcurrentBag<VoidResult>();

		await Parallel.ForAsync(0, attempts, async (_, _) =>
		{
			var result = await asyncLazy;
			bag.Add(result);
		});

		VoidResult.GetCounter().ShouldBeGreaterThan(1);
		VoidResult.GetCounter().ShouldBeLessThanOrEqualTo(attempts);
		asyncLazy.HasFactory.ShouldBeFalse();
		bag.Distinct().Count().ShouldBe(1);
	}

	[Fact]
	public async Task GetPublicationOnly_DoesNotPublish_OnError()
	{
		const int attempts = 10;
		const int sleep = 30;
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), Mode);
		var bag = new ConcurrentBag<CtorException>();

		await Parallel.ForAsync(0, attempts, async (_, _) =>
		{
			try
			{
				await asyncLazy;
			}
			catch (CtorException e)
			{
				bag.Add(e);
			}
		});

		CtorException.GetCounter().ShouldBeGreaterThan(1);
		CtorException.GetCounter().ShouldBeLessThanOrEqualTo(attempts);
		asyncLazy.HasFactory.ShouldBeTrue();
		bag.Distinct().Count().ShouldBe(bag.Count, "Each failure is the result of a new unsuccessful attempt.");
	}

	[Fact]
	public async Task GetPublicationOnly_DoesNotPublish_OnFactoryError()
	{
		const int attempts = 10;
		var asyncLazy = new AsyncLazy<VoidResult>(_ => CtorException.ThrowsDirectly(), Mode);
		var bag = new ConcurrentBag<CtorException>();

		await Parallel.ForAsync(0, attempts, async (_, _) =>
		{
			try
			{
				await asyncLazy;
			}
			catch (CtorException e)
			{
				bag.Add(e);
			}
		});

		CtorException.GetCounter().ShouldBeGreaterThan(1);
		CtorException.GetCounter().ShouldBeLessThanOrEqualTo(attempts);
		asyncLazy.HasFactory.ShouldBeTrue();
		bag.Distinct().Count().ShouldBe(bag.Count, "Each failure is the result of a new unsuccessful attempt.");
	}

	[Fact]
	public async Task GetPublicationOnly_ResetEvenIfNotAwaited_OnError()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), Mode);

		const int attempts = 3;
		for (var i = 0; i < attempts; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
			asyncLazy.IsValueCreated.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Fact]
	public async Task GetPublicationOnly_ResetEvenIfNotAwaited_OnFactoryError()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(_ => CtorException.ThrowsDirectly(), Mode);

		const int attempts = 3;
		for (var i = 0; i < attempts; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
			asyncLazy.IsValueCreated.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}
}