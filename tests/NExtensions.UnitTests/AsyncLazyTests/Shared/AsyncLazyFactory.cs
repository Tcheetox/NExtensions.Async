using NExtensions.Async;

namespace NExtensions.UnitTests.AsyncLazyTests.Shared;

public static class AsyncLazyFactory
{
	public static IEnumerable<object[]> WithOrWithoutCancellation =>
		new List<object[]>
		{
			new object[] { true },
			new object[] { false }
		};

	public static AsyncLazy<T> Create<T>(Func<CancellationToken, Task<T>> factory, bool withCancellation, LazyAsyncSafetyMode mode)
	{
		return withCancellation ? new AsyncLazy<T>(factory, mode) : new AsyncLazy<T>(() => factory(CancellationToken.None), mode);
	}
}