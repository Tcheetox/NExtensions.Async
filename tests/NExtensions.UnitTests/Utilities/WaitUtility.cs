namespace NExtensions.UnitTests.Utilities;

public static class WaitUtility
{
	public static async Task WaitUntil(Func<bool> condition)
	{
		ArgumentNullException.ThrowIfNull(condition);

		var delay = TimeSpan.FromMilliseconds(10); // Polling interval
		while (!condition())
		{
			await Task.Delay(delay);
		}
	}
}