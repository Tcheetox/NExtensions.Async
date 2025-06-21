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

		VoidResult.Counter.ShouldBe(1);
	}

	[Fact]
	public async Task GetPublicationOnly_CreatesOnce_OnError()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.Counter.ShouldBe(1);
	}

	[Fact]
	public async Task GetPublicationOnly_CreatesOnce_OnFactoryError()
	{
		const int attempts = 10;
		var asyncLazy = new AsyncLazy<VoidResult>(_ => CtorException.ThrowsDirectly(), Mode);
		var bag = new ConcurrentBag<CtorException>();

		for (var i = 0; i < attempts; i++)
		{
			try
			{
				await asyncLazy;
			}
			catch (CtorException e)
			{
				bag.Add(e);
			}

			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.Counter.ShouldBeGreaterThanOrEqualTo(1);
		bag.Count.ShouldBe(attempts);
		bag.Distinct().Count().ShouldBe(1);
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

		VoidResult.Counter.ShouldBeGreaterThanOrEqualTo(1);
		VoidResult.Counter.ShouldBeLessThanOrEqualTo(attempts);
		asyncLazy.HasFactory.ShouldBeFalse();
		bag.Distinct().Count().ShouldBe(1);
	}

	[Fact]
	public async Task GetPublicationOnly_PublishesOnce_OnError()
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

		CtorException.Counter.ShouldBeGreaterThanOrEqualTo(1);
		CtorException.Counter.ShouldBeLessThanOrEqualTo(attempts);
		asyncLazy.HasFactory.ShouldBeFalse();
		bag.Distinct().Count().ShouldBe(1);
	}
}