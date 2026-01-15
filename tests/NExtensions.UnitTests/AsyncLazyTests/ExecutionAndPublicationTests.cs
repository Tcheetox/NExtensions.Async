using System.Collections.Concurrent;
using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using NExtensions.UnitTests.Utilities;

// ReSharper disable RedundantArgumentDefaultValue

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class ExecutionAndPublicationTests : NonParallelTests
{
	private const LazyAsyncSafetyMode Mode = LazyAsyncSafetyMode.ExecutionAndPublication;

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetExecutionAndPublication_CreatesOnce_OnSuccess(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create(token => VoidResult.GetAsync(5, token), withCancellation, Mode);
		for (var i = 0; i < 3; i++)
		{
			_ = await asyncLazy;
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		VoidResult.GetCounter().ShouldBe(1);
		asyncLazy.IsValueCreated.ShouldBeTrue();
		asyncLazy.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetExecutionAndPublication_CreatesOnce_OnError(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create(token => CtorException.ThrowsAsync(5, token), withCancellation, Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(1);
		asyncLazy.IsValueCreated.ShouldBeTrue();
		asyncLazy.IsFaulted.ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetExecutionAndPublication_CreatesOnce_OnFactoryError(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(_ => CtorException.ThrowsDirectly(), withCancellation, Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(1);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetExecutionAndPublication_PublishesAndCreatesOnce_OnSuccess(bool withCancellation)
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

		VoidResult.GetCounter().ShouldBe(1);
		asyncLazy.HasFactory.ShouldBeFalse();
		bag.Distinct().Count().ShouldBe(1);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetExecutionAndPublication_PublishesOnce_OnError(bool withCancellation)
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

		CtorException.GetCounter().ShouldBe(1);
		asyncLazy.HasFactory.ShouldBeFalse();
		bag.Distinct().Count().ShouldBe(1);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetExecutionAndPublication_PublishesOnce_OnFactoryError(bool withCancellation)
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

		CtorException.GetCounter().ShouldBe(1);
		asyncLazy.HasFactory.ShouldBeFalse();
		bag.Distinct().Count().ShouldBe(1);
	}
}