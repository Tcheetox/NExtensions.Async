using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class NoneTests : NonParallelTests
{
	private const LazyAsyncSafetyMode Mode = LazyAsyncSafetyMode.None;

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetNone_CreatesOnce_OnSuccess(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => VoidResult.GetAsync(5, token), withCancellation, Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.NotThrowAsync(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		VoidResult.GetCounter().ShouldBe(1);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetNone_CreatesOnce_OnError(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(token => CtorException.ThrowsAsync(5, token), withCancellation, Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(1);
	}

	[Theory]
	[MemberData(nameof(AsyncLazyFactory.WithOrWithoutCancellation), MemberType = typeof(AsyncLazyFactory))]
	public async Task GetNone_CreatesOnce_OnFactoryError(bool withCancellation)
	{
		var asyncLazy = AsyncLazyFactory.Create<VoidResult>(_ => CtorException.ThrowsDirectly(), withCancellation, Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(1);
	}
}