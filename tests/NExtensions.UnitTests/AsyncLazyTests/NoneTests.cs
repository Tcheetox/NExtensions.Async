using NExtensions.Async;
using NExtensions.UnitTests.AsyncLazyTests.Shared;
using Shouldly;

namespace NExtensions.UnitTests.AsyncLazyTests;

[Collection("NonParallelTests")]
public class NoneTests : NonParallelTests
{
	private const LazyAsyncThreadSafetyMode Mode = LazyAsyncThreadSafetyMode.None;

	[Fact]
	public async Task GetNoneAsync_CreatesOnce_OnSuccess()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => VoidResult.GetAsync(5, token), Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.NotThrowAsync(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		VoidResult.GetCounter().ShouldBe(1);
	}

	[Fact]
	public async Task GetNoneAsync_CreatesOnce_OnError()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(token => CtorException.ThrowsAsync(5, token), Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(1);
	}

	[Fact]
	public async Task GetNoneAsync_CreatesOnce_OnFactoryError()
	{
		var asyncLazy = new AsyncLazy<VoidResult>(_ => CtorException.ThrowsDirectly(), Mode);

		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<CtorException>(async () => await asyncLazy);
			asyncLazy.HasFactory.ShouldBeFalse();
		}

		CtorException.GetCounter().ShouldBe(1);
	}
}