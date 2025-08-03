namespace NExtensions.UnitTests.Utilities;

public static class WaitUtility
{
	public static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(condition);

		var pollingInterval = TimeSpan.FromMilliseconds(10);
		var startTime = DateTime.UtcNow;

		while (!condition())
		{
			if (DateTime.UtcNow - startTime > timeout)
				return; // Exit silently on timeout
			await Task.Delay(pollingInterval);
		}
	}
}