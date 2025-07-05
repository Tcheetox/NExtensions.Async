namespace NExtensions.UnitTests.Utilities;

internal static class InterlockedUtility
{
	public static void Max(ref int target, int value)
	{
		int initialValue, computedValue;
		do
		{
			initialValue = Volatile.Read(ref target);
			computedValue = Math.Max(initialValue, value);
			if (initialValue == computedValue)
				return; // Already at or above value
		} while (Interlocked.CompareExchange(ref target, computedValue, initialValue) != initialValue);
	}
}