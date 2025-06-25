namespace NExtensions.UnitTests.Utilities;

internal static class ParallelUtility
{
	public static Task ForAsync(int fromInclusive, int toExclusive, Func<int, CancellationToken, ValueTask> body)
	{
#if NET8_0_OR_GREATER
		return Parallel.ForAsync(fromInclusive, toExclusive, body);
#else
		ArgumentNullException.ThrowIfNull(body);
		if (fromInclusive > toExclusive)
			throw new ArgumentOutOfRangeException(nameof(fromInclusive), $"'{nameof(fromInclusive)}' must be less than or equal to '{nameof(toExclusive)}'.");

		var range = Enumerable.Range(fromInclusive, toExclusive - fromInclusive);
		return Parallel.ForEachAsync(range, body);
#endif
	}
}