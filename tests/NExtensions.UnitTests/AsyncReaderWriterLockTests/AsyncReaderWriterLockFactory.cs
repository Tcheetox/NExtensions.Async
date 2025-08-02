using NExtensions.Async;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public static class AsyncReaderWriterLockFactory
{
	public static IEnumerable<object[]> ReaderWriterOptions =>
		new List<object[]>
		{
			new object[] { false, false },
			new object[] { false, true },
			new object[] { true, false },
			new object[] { true, true }
		};

	public static AsyncReaderWriterLock Create(bool syncReader, bool syncWriter)
	{
		return new AsyncReaderWriterLock(syncReader, syncWriter);
	}
}