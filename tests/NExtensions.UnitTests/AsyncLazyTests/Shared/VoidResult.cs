namespace NExtensions.UnitTests.AsyncLazyTests.Shared;

public class VoidResult
{
	private static int _counter;

	public VoidResult()
	{
		Interlocked.Increment(ref _counter);
	}

	public static int Counter => _counter;

	public static async Task<VoidResult> GetAsync(int sleep, CancellationToken cancellationToken = default)
	{
		if (sleep == 0)
			await Task.Yield();
		else
			await Task.Delay(sleep, cancellationToken);
		return new VoidResult();
	}

	public static void Reset()
	{
		Interlocked.Exchange(ref _counter, 0);
	}
}