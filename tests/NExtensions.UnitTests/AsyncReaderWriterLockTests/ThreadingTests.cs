using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public class ThreadingTests
{
	[Fact]
	public async Task ConcurrentReadersAndWriters_ShouldMaintainDataIntegrity()
	{
		var rwLock = new AsyncReaderWriterLock();
		var sharedCounter = 0;
		const int readerIterations = 1000;
		const int writerIterations = 100;
		const int workers = 10;

		var readers = Enumerable
			.Range(0, workers)
			.Select(_ => Task.Run(async () =>
			{
				for (var i = 0; i < readerIterations; i++)
				{
					using (await rwLock.ReaderLockAsync())
					{
						var val1 = sharedCounter;
						await Task.Yield();
						var val2 = sharedCounter;
						val1.ShouldBe(val2);
					}
				}
			}));

		var writers = Enumerable
			.Range(0, workers)
			.Select(_ => Task.Run(async () =>
			{
				for (var i = 0; i < writerIterations; i++)
				{
					using (await rwLock.WriterLockAsync())
					{
						sharedCounter++;
						await Task.Yield();
					}
				}
			}));

		// Run all readers and writers concurrently
		await Task.WhenAll(readers.Concat(writers));
		sharedCounter.ShouldBe(workers * writerIterations);
	}
}