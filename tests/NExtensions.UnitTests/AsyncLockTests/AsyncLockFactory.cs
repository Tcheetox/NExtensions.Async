using NExtensions.Async;

namespace NExtensions.UnitTests.AsyncLockTests;

public static class AsyncLockFactory
{
	public static IEnumerable<object[]> ContinuationOptions =>
		new List<object[]>
		{
			new object[] { true },
			new object[] { false }
		};

	public static AsyncLock Create(bool syncContinuation)
	{
		return new AsyncLock(syncContinuation);
	}
}