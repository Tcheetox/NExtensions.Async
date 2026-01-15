using System.Collections.Concurrent;
using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using NExtensions.UnitTests.Utilities;

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class PublicationOnlyTests : NonParallelTests
{
	private const LazyAsyncSafetyMode Mode = LazyAsyncSafetyMode.PublicationOnly;

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetPublicationOnly_CreatesOnce_OnSuccess(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => VoidResult.GetAsync(5, token), withCancellation, Mode);

		for (var i = 0; i < 3; i++)
		{
			_ = await asyncLazy;
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		VoidResult.GetCounter().ShouldBe(1);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetPublicationOnly_Retries_OnError(bool withCancellation)
	{
		const int attempts = 3;
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(5, token), withCancellation, Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetPublicationOnly_Retries_OnFactoryError(bool withCancellation)
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
	public async Task GetPublicationOnly_PublishesOnce_OnSuccess(bool withCancellation)
	{
		const int attempts = 10;
		const int sleep = 30;
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => VoidResult.GetAsync(sleep, token), withCancellation, Mode);
		var bag = new ConcurrentBag<VoidResult>();

		await ParallelUtility.ForAsync(0, attempts, async (_, _) =>
		{
			var result = await asyncLazy;
			bag.Add(result);
		});

		VoidResult.GetCounter().ShouldBeGreaterThan(1);
		VoidResult.GetCounter().ShouldBeLessThanOrEqualTo(attempts);
		asyncLazy.HasFactory.ShouldBeFalse();
		bag.Distinct().Count().ShouldBe(1);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetPublicationOnly_DoesNotPublish_OnError(bool withCancellation)
	{
		const int attempts = 10;
		const int sleep = 30;
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), withCancellation, Mode);
		var bag = new ConcurrentBag<CtorException>();

		await ParallelUtility.ForAsync(0, attempts, async (_, _) =>
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

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetPublicationOnly_DoesNotPublish_OnFactoryError(bool withCancellation)
	{
		const int attempts = 10;
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(_ => CtorException.ThrowsDirectly(), withCancellation, Mode);
		var bag = new ConcurrentBag<CtorException>();

		await ParallelUtility.ForAsync(0, attempts, async (_, _) =>
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

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetPublicationOnly_ResetEvenIfNotAwaited_OnError(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(5, token), withCancellation, Mode);

		const int attempts = 3;
		for (var i = 0; i < attempts; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeTrue();
			asyncLazy.IsValueCreated.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(attempts);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetPublicationOnly_ResetEvenIfNotAwaited_OnFactoryError(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(_ => CtorException.ThrowsDirectly(), withCancellation, Mode);

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