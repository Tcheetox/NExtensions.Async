using System.Collections.Concurrent;
using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using NExtensions.UnitTests.Utilities;
using Shouldly;

// ReSharper disable RedundantArgumentDefaultValue

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class ExecutionAndPublicationTests : NonParallelTests
{
	private const LazyAsyncThreadSafetyMode Mode = LazyAsyncThreadSafetyMode.ExecutionAndPublication;

	[Fact]
	public async Task GetExecutionAndPublication_CreatesOnce_OnSuccess()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), Mode);

		for (var i = 0; i < 3; i++)
		{
			_ = await asyncLazy;
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		VoidResult.GetCounter().ShouldBe(1);
		asyncLazy.IsValueCreated.ShouldBeTrue();
		asyncLazy.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Fact]
	public async Task GetExecutionAndPublication_CreatesOnce_OnError()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(1);
		asyncLazy.IsValueCreated.ShouldBeTrue();
		asyncLazy.IsFaulted.ShouldBeTrue();
	}

	[Fact]
	public async Task GetExecutionAndPublication_CreatesOnce_OnFactoryError()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(_ => CtorException.ThrowsDirectly(), Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(1);
	}

	[Fact]
	public async Task GetExecutionAndPublication_PublishesAndCreatesOnce_OnSuccess()
	{
		const int attempts = 10;
		const int sleep = 30;
		var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(sleep, token), Mode);
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

	[Fact]
	public async Task GetExecutionAndPublication_PublishesOnce_OnError()
	{
		const int attempts = 10;
		const int sleep = 30;
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(sleep, token), Mode);
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

	[Fact]
	public async Task GetExecutionAndPublication_PublishesOnce_OnFactoryError()
	{
		const int attempts = 10;
		var asyncLazy = new AsyncLazy<VoidResult>(_ => CtorException.ThrowsDirectly(), Mode);
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