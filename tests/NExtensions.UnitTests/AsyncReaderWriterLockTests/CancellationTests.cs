using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public class CancellationTests
{
	[Fact]
	public async Task ReaderLockAsync_DoesNotBlockQueuedWriter_WhenCancelledOnEnqueue()
	{
		var rwLock = new AsyncReaderWriterLock();

		using var cts = new CancellationTokenSource(TimeSpan.Zero);
		var reader = rwLock.ReaderLockAsync(cts.Token);
		reader.IsCompleted.ShouldBeTrue();
		reader.IsCanceled.ShouldBeTrue();

		var writerTask = rwLock.WriterLockAsync(CancellationToken.None);
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask).Dispose();
	}

	[Fact]
	public async Task WriterLockAsync_DoesNotBlockQueuedWriter_WhenCancelledOnEnqueue()
	{
		var rwLock = new AsyncReaderWriterLock();

		using var cts = new CancellationTokenSource(TimeSpan.Zero);
		var writer = rwLock.WriterLockAsync(cts.Token);
		writer.IsCompleted.ShouldBeTrue();
		writer.IsCanceled.ShouldBeTrue();

		var writerTask = rwLock.WriterLockAsync(CancellationToken.None);
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask).Dispose();
	}

	[Fact]
	public async Task WriterLockAsync_DoesNotBlockQueuedReader_WhenCancelledOnEnqueue()
	{
		var rwLock = new AsyncReaderWriterLock();

		using var cts = new CancellationTokenSource(TimeSpan.Zero);
		var writer = rwLock.WriterLockAsync(cts.Token);
		writer.IsCompleted.ShouldBeTrue();
		writer.IsCanceled.ShouldBeTrue();

		var readerTask = rwLock.ReaderLockAsync(CancellationToken.None);
		readerTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await readerTask).Dispose();
	}

	[Fact]
	public async Task ReaderLockAsync_ReleaseQueuedWriter_WhenCancelledThenDisposedWhileEnqueued()
	{
		var rwLock = new AsyncReaderWriterLock();

		var reader1 = await rwLock.ReaderLockAsync();

		using var cts = new CancellationTokenSource();
		var reader2Task = rwLock.ReaderLockAsync(cts.Token);
		var writerTask = rwLock.WriterLockAsync(CancellationToken.None);

		// Cancel the second reader (the reader should have return directly anyway)
		reader2Task.IsCompletedSuccessfully.ShouldBeTrue();
		await cts.CancelAsync(); // It's had no effect since it completed synchronously
		reader2Task.IsCanceled.ShouldBeFalse();
		var reader2 = await reader2Task;
		reader1.Dispose();
		writerTask.IsCompletedSuccessfully.ShouldBeFalse(); // Because reader2 is not yet disposed and got acquired synchronously
		reader2.Dispose();
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		var writer = await writerTask;
		Should.NotThrow(() => writer.Dispose());
	}

	[Fact]
	public async Task WriterLockAsync_ReleaseQueuedWriter_WhenCancelledWhileEnqueued()
	{
		var rwLock = new AsyncReaderWriterLock();
		var reader1 = await rwLock.ReaderLockAsync();
		using var cts = new CancellationTokenSource(30);
		cts.Token.Register(() => reader1.Dispose());
		var writerLock = rwLock.WriterLockAsync(cts.Token);
		var writer2 = rwLock.WriterLockAsync(CancellationToken.None);
		writer2.IsCompleted.ShouldBeFalse();
		await Task.Delay(50, CancellationToken.None);
		await Should.ThrowAsync<OperationCanceledException>(async () => await writerLock);
		writer2.IsCompletedSuccessfully.ShouldBeTrue("The cancelled writer must have released the enqueued writer");
	}

	[Fact]
	public async Task WriterLockAsync_ReleaseQueuedReader_WhenCancelledWhileEnqueued()
	{
		var rwLock = new AsyncReaderWriterLock();

		var reader1 = await rwLock.ReaderLockAsync();
		using var cts = new CancellationTokenSource(30);
		var writerLock = rwLock.WriterLockAsync(cts.Token);
		var reader2 = rwLock.ReaderLockAsync(CancellationToken.None);
		reader2.IsCompleted.ShouldBeFalse();
		await Task.Delay(50, CancellationToken.None);
		reader2.IsCompletedSuccessfully.ShouldBeTrue("The cancelled writer must have released the enqueued readers");
		await Should.ThrowAsync<OperationCanceledException>(async () => await writerLock);
		reader1.Dispose();
	}
}