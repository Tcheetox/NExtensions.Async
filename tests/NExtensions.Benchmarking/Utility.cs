using System.Runtime.CompilerServices;

namespace NExtensions.Benchmarking;

internal static class Utility
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task WaitMeAsync(string wait)
	{
		switch (wait)
		{
			case "yield":
				await Task.Yield();
				return;
			case "delay":
				await Task.Delay(1);
				return;
			case "sync":
				return;
			default:
				throw new NotImplementedException($"Waiting for {wait} is not implemented");
		}
	}
}