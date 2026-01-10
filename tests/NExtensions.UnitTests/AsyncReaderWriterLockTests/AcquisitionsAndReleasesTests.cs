using Shouldly;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public class AcquisitionsAndReleasesTests
{
	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterReaderScopeAsync_ShouldAcquireImmediately_WhenNoWriterOrQueuedWriters(bool syncReader, bool syncWriter)
	{
		// Arrange
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		// Act
		var task = rwLock.EnterReaderScopeAsync();
		var acquired = task.IsCompletedSuccessfully;
		var releaser = await task;
		await rwLock.EnterReaderScopeAsync(); // Another one

		// Assert
		acquired.ShouldBeTrue();
		Should.NotThrow(() => releaser.Dispose());
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ShouldAcquireImmediately_WhenNoReadersOrActiveWriter(bool syncReader, bool syncWriter)
	{
		// Arrange
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		// Act
		var task = rwLock.EnterWriterScopeAsync();
		var acquired = task.IsCompletedSuccessfully;

		// Assert
		acquired.ShouldBeTrue();

		var releaser = await task;
		Should.NotThrow(() => releaser.Dispose());
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ShouldAcquireManyTimes_WithoutStackDiveIssue(bool syncReader, bool syncWriter)
	{
		// Arrange
		const int toAcquire = 10_000_000;
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);
		var acquired = 0;

		// Act
		for (var i = 0; i < toAcquire; i++)
		{
			using (await rwLock.EnterWriterScopeAsync())
			{
				acquired++;
			}
		}

		// Assert
		acquired.ShouldBe(toAcquire);
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterReaderScopeAsync_ShouldQueue_WhenWriterActiveOrQueuedWritersExist(bool syncReader, bool syncWriter)
	{
		// Arrange
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);
		var writerReleaser = await rwLock.EnterWriterScopeAsync();

		// Act
		var readerTask = rwLock.EnterReaderScopeAsync();

		// Assert
		readerTask.IsCompleted.ShouldBeFalse();
		writerReleaser.Dispose();
		readerTask.IsCompletedSuccessfully.ShouldBeTrue();
		var readerReleaser = await readerTask;
		Should.Throw<InvalidOperationException>(() => readerTask.IsCompleted, "The result has been consumed, thus internal state is reset.");
		Should.NotThrow(() => readerReleaser.Dispose());
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ShouldBeQueued_WhenWriterActiveOrReadersExist(bool syncReader, bool syncWriter)
	{
		// Arrange
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);
		var readerReleaser = await rwLock.EnterReaderScopeAsync();

		// Act && Assert
		var writerTask = rwLock.EnterWriterScopeAsync();
		writerTask.IsCompleted.ShouldBeFalse("Writer should wait because a reader is active.");
		readerReleaser.Dispose();
		writerTask.IsCompleted.ShouldBeTrue("Writer lock should complete after readers release.");
		var writerReleaser = await writerTask;
		Should.Throw<InvalidOperationException>(() => writerTask.IsCompleted, "The result has been consumed, thus internal state is reset.");
		Should.NotThrow(() => writerReleaser.Dispose());
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ShouldResume_WhenAllReadersReleased(bool syncReader, bool syncWriter)
	{
		// Arrange
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		// Act
		var reader1 = await rwLock.EnterReaderScopeAsync();
		var reader2 = await rwLock.EnterReaderScopeAsync();

		// Assert
		var writerTask = rwLock.EnterWriterScopeAsync();
		writerTask.IsCompleted.ShouldBeFalse();

		// Release both readers
		reader1.Dispose();
		reader2.Dispose();
		var writerReleaser = await writerTask;
		writerReleaser.Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ShouldSkipCancelledWriters_WhenReleased(bool syncReader, bool syncWriter)
	{
		// Arrange
		const int cancelAfter = 300;
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		// Act
		var writer1 = await rwLock.EnterWriterScopeAsync();
		var cts = new CancellationTokenSource(cancelAfter);
		_ = rwLock.EnterWriterScopeAsync(cts.Token).AsTask();
		var readerTask = rwLock.EnterReaderScopeAsync(CancellationToken.None);
		await Task.Delay(cancelAfter * 2, CancellationToken.None);
		writer1.Dispose();

		// Assert
		readerTask.IsCompleted.ShouldBeTrue();
		cts.Dispose();
	}


	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ShouldResumeNextWriter_WhenMultipleWritersQueued(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		// First writer acquires the lock
		var firstWriter = await rwLock.EnterWriterScopeAsync();

		// Queue two writers behind
		var writerTask1 = rwLock.EnterWriterScopeAsync();
		var writerTask2 = rwLock.EnterWriterScopeAsync();
		writerTask1.IsCompleted.ShouldBeFalse();
		writerTask2.IsCompleted.ShouldBeFalse();

		// Release the first writer, next writer should resume
		firstWriter.Dispose();
		writerTask1.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask1).Dispose();
		writerTask2.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask2).Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ShouldResumeNextReaders_WhenMultipleReadersQueued(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		// The first writer acquires the lock
		var firstWriter = await rwLock.EnterWriterScopeAsync();

		// Queue two readers behind (one will expire while in the queue)
		var readerTask1 = rwLock.EnterReaderScopeAsync();
		using var cts = new CancellationTokenSource(10);
		var readerTask2 = rwLock.EnterReaderScopeAsync(cts.Token);
		await Task.Delay(100, CancellationToken.None);
		readerTask1.IsCompleted.ShouldBeFalse();
		readerTask2.IsCanceled.ShouldBeTrue();

		// Release the first writer, next writer should resume
		firstWriter.Dispose();
		readerTask1.IsCompletedSuccessfully.ShouldBeTrue();
		(await readerTask1).Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterReaderScopeAsync_ShouldResumeOnly_AfterAllWritersReleased(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		var writer1 = await rwLock.EnterWriterScopeAsync();
		var writer2Task = rwLock.EnterWriterScopeAsync();
		var readerTask = rwLock.EnterReaderScopeAsync();

		writer1.Dispose();
		(await writer2Task).Dispose();
		readerTask.IsCompleted.ShouldBeTrue();
		(await readerTask).Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ShouldTakePriority_OverSubsequentReaders(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		// First reader acquires lock immediately
		var reader1 = await rwLock.EnterReaderScopeAsync();
		var writerTask = rwLock.EnterWriterScopeAsync();
		var reader2Task = rwLock.EnterReaderScopeAsync();

		// Release first reader, next writer should acquire lock next, not reader2
		reader1.Dispose();
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask).Dispose();
		reader2Task.IsCompletedSuccessfully.ShouldBeTrue();
		(await reader2Task).Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ThrowsObjectDisposedException_WhenDisposedTwice(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		var writer = await rwLock.EnterWriterScopeAsync();
		writer.Dispose();

		Should.Throw<ObjectDisposedException>(() => writer.Dispose());
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterReaderScopeAsync_ThrowsObjectDisposedException_WhenDisposedTwice(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		var reader = await rwLock.EnterReaderScopeAsync();
		reader.Dispose();

		Should.Throw<ObjectDisposedException>(() => reader.Dispose());
	}
}