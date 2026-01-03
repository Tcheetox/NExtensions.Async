using System.Diagnostics;

namespace NExtensions.UnitTests.Utilities;

internal static class StopwatchUtility
{
#if !NET7_0_OR_GREATER
	private static readonly double TickDuration = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
#endif

	public static TimeSpan GetElapsedTime(long started, long ended)
	{
#if NET7_0_OR_GREATER
		return Stopwatch.GetElapsedTime(started, ended);
#else
		return TimeSpan.FromSeconds((double)(ended - started) / Stopwatch.Frequency);
#endif
	}

	public static TimeSpan FromTicks(long stopwatchTicks)
	{
#if NET7_0_OR_GREATER
		return Stopwatch.GetElapsedTime(0, stopwatchTicks);
#else
		var scaledTicks = (long)(stopwatchTicks * TickDuration);
		return TimeSpan.FromTicks(scaledTicks);
#endif
	}
}