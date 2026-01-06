namespace NExtensions.UnitTests.AsyncAutoResetEventTests;

public class AsyncAutoResetEventFactory
{
	public static IEnumerable<object[]> Permutations =>
		new List<object[]>
		{
			new object[] { true, true },
			new object[] { true, false },
			new object[] { false, false },
			new object[] { false, true }
		};

	public static IEnumerable<object[]> ContinuationOptions =>
		new List<object[]>
		{
			new object[] { true },
			new object[] { false }
		};
}