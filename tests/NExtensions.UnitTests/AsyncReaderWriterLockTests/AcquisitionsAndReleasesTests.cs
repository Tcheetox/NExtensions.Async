using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public class AcquisitionsAndReleasesTests
{
	[Fact]
	public async Task ReaderLockAsync_ShouldAcquireImmediately_WhenNoWriterOrQueuedWriters()
	{
		// Arrange
		var rwLock = new AsyncReaderWriterLock();

		// Act
		var task = rwLock.ReaderLockAsync();
		var acquired = task.IsCompletedSuccessfully;
		var releaser = await task;
		await rwLock.ReaderLockAsync(); // Another one

		// Assert
		acquired.ShouldBeTrue();
		Should.NotThrow(() => releaser.Dispose());
	}

	[Fact]
	public async Task WriterLockAsync_ShouldAcquireImmediately_WhenNoReadersOrActiveWriter()
	{
		// Arrange
		var rwLock = new AsyncReaderWriterLock();

		// Act
		var task = rwLock.WriterLockAsync();
		var acquired = task.IsCompletedSuccessfully;

		// Assert
		acquired.ShouldBeTrue();

		var releaser = await task;
		Should.NotThrow(() => releaser.Dispose());
	}

	[Fact]
	public async Task ReaderLockAsync_ShouldQueue_WhenWriterActiveOrQueuedWritersExist()
	{
		// Arrange
		var rwLock = new AsyncReaderWriterLock();
		var writerReleaser = await rwLock.WriterLockAsync();

		// Act
		var readerTask = rwLock.ReaderLockAsync();

		// Assert
		readerTask.IsCompleted.ShouldBeFalse();
		writerReleaser.Dispose();
		readerTask.IsCompletedSuccessfully.ShouldBeTrue();
		var readerReleaser = await readerTask;
		Should.Throw<InvalidOperationException>(() => readerTask.IsCompleted, "The result has been consumed, thus internal state is reset.");
		Should.NotThrow(() => readerReleaser.Dispose());
	}

	[Fact]
	public async Task WriterLockAsync_ShouldBeQueued_WhenWriterActiveOrReadersExist()
	{
		// Arrange
		var rwLock = new AsyncReaderWriterLock();
		var readerReleaser = await rwLock.ReaderLockAsync();

		// Act && Assert
		var writerTask = rwLock.WriterLockAsync();
		writerTask.IsCompleted.ShouldBeFalse("Writer should wait because a reader is active.");
		readerReleaser.Dispose();
		writerTask.IsCompleted.ShouldBeTrue("Writer lock should complete after readers release.");
		var writerReleaser = await writerTask;
		Should.Throw<InvalidOperationException>(() => writerTask.IsCompleted, "The result has been consumed, thus internal state is reset.");
		Should.NotThrow(() => writerReleaser.Dispose());
	}

	[Fact]
	public async Task WriterLockAsync_ShouldResume_WhenAllReadersReleased()
	{
		// Arrange
		var rwLock = new AsyncReaderWriterLock();

		// Act
		var reader1 = await rwLock.ReaderLockAsync();
		var reader2 = await rwLock.ReaderLockAsync();

		// Assert
		var writerTask = rwLock.WriterLockAsync();
		writerTask.IsCompleted.ShouldBeFalse();

		// Release both readers
		reader1.Dispose();
		reader2.Dispose();
		var writerReleaser = await writerTask;
		writerReleaser.Dispose();
	}

	[Fact]
	public async Task WriterLockAsync_ShouldSkipCancelledWriters_WhenReleased()
	{
		// Arrange
		const int cancelAfter = 300;
		var rwLock = new AsyncReaderWriterLock();

		// Act
		var writer1 = await rwLock.WriterLockAsync();
		var cts = new CancellationTokenSource(cancelAfter);
		_ = rwLock.WriterLockAsync(cts.Token).AsTask();
		var readerTask = rwLock.ReaderLockAsync(CancellationToken.None);
		await Task.Delay(cancelAfter * 2, CancellationToken.None);
		writer1.Dispose();

		// Assert
		readerTask.IsCompleted.ShouldBeTrue();
		cts.Dispose();
	}


	[Fact]
	public async Task WriterLockAsync_ShouldResumeNextWriter_WhenMultipleWritersQueued()
	{
		var rwLock = new AsyncReaderWriterLock();

		// First writer acquires the lock
		var firstWriter = await rwLock.WriterLockAsync();

		// Queue two writers behind
		var writerTask1 = rwLock.WriterLockAsync();
		var writerTask2 = rwLock.WriterLockAsync();
		writerTask1.IsCompleted.ShouldBeFalse();
		writerTask2.IsCompleted.ShouldBeFalse();

		// Release the first writer, next writer should resume
		firstWriter.Dispose();
		writerTask1.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask1).Dispose();
		writerTask2.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask2).Dispose();
	}

	[Fact]
	public async Task ReaderLockAsync_ShouldResumeOnly_AfterAllWritersReleased()
	{
		var rwLock = new AsyncReaderWriterLock();

		var writer1 = await rwLock.WriterLockAsync();
		var writer2Task = rwLock.WriterLockAsync();
		var readerTask = rwLock.ReaderLockAsync();

		writer1.Dispose();
		(await writer2Task).Dispose();
		readerTask.IsCompleted.ShouldBeTrue();
		(await readerTask).Dispose();
	}

	[Fact]
	public async Task WriterLockAsync_ShouldTakePriority_OverSubsequentReaders()
	{
		var rwLock = new AsyncReaderWriterLock();

		// First reader acquires lock immediately
		var reader1 = await rwLock.ReaderLockAsync();
		var writerTask = rwLock.WriterLockAsync();
		var reader2Task = rwLock.ReaderLockAsync();

		// Release first reader, next writer should acquire lock next, not reader2
		reader1.Dispose();
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask).Dispose();
		reader2Task.IsCompletedSuccessfully.ShouldBeTrue();
		(await reader2Task).Dispose();
	}

	[Fact]
	public async Task WriterLockAsync_ThrowsObjectDisposedException_WhenDisposedTwice()
	{
		var rwLock = new AsyncReaderWriterLock();

		var writer = await rwLock.WriterLockAsync();
		writer.Dispose();

		Should.Throw<ObjectDisposedException>(() => writer.Dispose());
	}

	[Fact]
	public async Task ReaderLockAsync_ThrowsObjectDisposedException_WhenDisposedTwice()
	{
		var rwLock = new AsyncReaderWriterLock();

		var reader = await rwLock.ReaderLockAsync();
		reader.Dispose();

		Should.Throw<ObjectDisposedException>(() => reader.Dispose());
	}
}