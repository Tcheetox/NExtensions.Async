namespace NExtensions.UnitTests.AsyncLazyTests.Shared;

public class CtorException : Exception
{
	private static int _counter;

	public CtorException() : base("Unlucky!")
	{
		Interlocked.Increment(ref _counter);
	}

	public static int Counter => _counter;

	public static async Task<VoidResult> ThrowsAsync(int sleep, CancellationToken cancellationToken = default)
	{
		if (sleep == 0)
			await Task.Yield();
		else
			await Task.Delay(sleep, cancellationToken);
		throw new CtorException();
	}

	public static Task<VoidResult> ThrowsDirectly()
	{
		throw new CtorException();
	}

	public static void Reset()
	{
		Interlocked.Exchange(ref _counter, 0);
	}
}