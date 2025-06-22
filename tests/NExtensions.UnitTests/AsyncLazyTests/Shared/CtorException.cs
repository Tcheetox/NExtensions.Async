using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NExtensions.UnitTests.AsyncLazyTests.Shared;

public class CtorException : Exception
{
	private static readonly ConcurrentDictionary<string, int> Counters = new(StringComparer.OrdinalIgnoreCase);

	public CtorException(string caller) : base("Unlucky!")
	{
		Counters.AddOrUpdate(caller, _ => 1, (_, value) => value + 1);
	}

	public static int GetCounter([CallerMemberName] string caller = null!)
	{
		return Counters.GetValueOrDefault(caller);
	}

	public static async Task<VoidResult> ThrowsAsync(int sleep, CancellationToken cancellationToken = default,
		[CallerMemberName] string? caller = null)
	{
		if (sleep == 0)
			await Task.Yield();
		else
			await Task.Delay(sleep, cancellationToken);
		throw new CtorException(caller!);
	}

	public static Task<VoidResult> ThrowsDirectly([CallerMemberName] string? caller = null)
	{
		throw new CtorException(caller!);
	}

	public static void Reset()
	{
		Counters.Clear();
	}
}