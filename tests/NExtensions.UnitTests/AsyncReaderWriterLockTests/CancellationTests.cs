using NExtensions.UnitTests.Utilities;
using Shouldly;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public class CancellationTests
{
	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterReaderScopeAsync_DoesNotBlockQueuedWriter_WhenCancelledOnEnqueue(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		using var cts = new CancellationTokenSource(TimeSpan.Zero);
		var reader = rwLock.EnterReaderScopeAsync(cts.Token);
		reader.IsCompleted.ShouldBeTrue();
		reader.IsCanceled.ShouldBeTrue();

		var writerTask = rwLock.EnterWriterScopeAsync(CancellationToken.None);
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask).Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_DoesNotBlockQueuedWriter_WhenCancelledOnEnqueue(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		using var cts = new CancellationTokenSource(TimeSpan.Zero);
		var writer = rwLock.EnterWriterScopeAsync(cts.Token);
		writer.IsCompleted.ShouldBeTrue();
		writer.IsCanceled.ShouldBeTrue();

		var writerTask = rwLock.EnterWriterScopeAsync(CancellationToken.None);
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await writerTask).Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_DoesNotBlockQueuedReader_WhenCancelledOnEnqueue(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		using var cts = new CancellationTokenSource(TimeSpan.Zero);
		var writer = rwLock.EnterWriterScopeAsync(cts.Token);
		writer.IsCompleted.ShouldBeTrue();
		writer.IsCanceled.ShouldBeTrue();

		var readerTask = rwLock.EnterReaderScopeAsync(CancellationToken.None);
		readerTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await readerTask).Dispose();
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterReaderScopeAsync_ReleaseQueuedWriter_WhenCancelledThenDisposedWhileEnqueued(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		var reader1 = await rwLock.EnterReaderScopeAsync();

		using var cts = new CancellationTokenSource();
		var reader2Task = rwLock.EnterReaderScopeAsync(cts.Token);
		var writerTask = rwLock.EnterWriterScopeAsync(CancellationToken.None);

		// Cancel the second reader (the reader should have return directly anyway)
		reader2Task.IsCompletedSuccessfully.ShouldBeTrue();
		cts.Cancel(); // It's had no effect since it completed synchronously
		reader2Task.IsCanceled.ShouldBeFalse();
		var reader2 = await reader2Task;
		reader1.Dispose();
		writerTask.IsCompletedSuccessfully.ShouldBeFalse(); // Because reader2 is not yet disposed and got acquired synchronously
		reader2.Dispose();
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		var writer = await writerTask;
		Should.NotThrow(() => writer.Dispose());
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ReleaseQueuedWriter_WhenCancelledWhileEnqueued(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);
		var reader1 = await rwLock.EnterReaderScopeAsync();
		using var cts = new CancellationTokenSource(30);
		cts.Token.Register(() => reader1.Dispose());
		var writerLock = rwLock.EnterWriterScopeAsync(cts.Token);
		var writer2 = rwLock.EnterWriterScopeAsync(CancellationToken.None);
		writer2.IsCompleted.ShouldBeFalse();
		await Task.Delay(50, CancellationToken.None);
		await Should.ThrowAsync<OperationCanceledException>(async () => await writerLock);
		writer2.IsCompletedSuccessfully.ShouldBeTrue("The cancelled writer must have released the enqueued writer");
	}

	[Theory]
	[MemberData(nameof(AsyncReaderWriterLockFactory.ReaderWriterOptions), MemberType = typeof(AsyncReaderWriterLockFactory))]
	public async Task EnterWriterScopeAsync_ReleaseQueuedReader_WhenCancelledWhileEnqueued(bool syncReader, bool syncWriter)
	{
		var rwLock = AsyncReaderWriterLockFactory.Create(syncReader, syncWriter);

		var reader1 = await rwLock.EnterReaderScopeAsync();
		using var cts = new CancellationTokenSource(30);
		var writerLock = rwLock.EnterWriterScopeAsync(cts.Token);
		var reader2 = rwLock.EnterReaderScopeAsync(CancellationToken.None);
		reader2.IsCompleted.ShouldBeFalse();
		await Task.Delay(50, CancellationToken.None);
		reader2.IsCompletedSuccessfully.ShouldBeTrue("The cancelled writer must have released the enqueued readers");
		await Should.ThrowAsync<OperationCanceledException>(async () => await writerLock);
		reader1.Dispose();
	}

	[Fact]
	public async Task EnterWriterScopeAsync_ReleaseQueuedReader_WhenCancelledWhileAboutToRun()
	{
		await ParallelUtility.ForAsync(0, 10, async (_, _) =>
		{
			for (var i = 0; i < 100; i += 2)
			{
				var rwLock = AsyncReaderWriterLockFactory.Create(false, false);
				var reader1 = await rwLock.EnterReaderScopeAsync(CancellationToken.None);
				using var preCts = new CancellationTokenSource(30);
				using var cts = new CancellationTokenSource(i);
				preCts.Token.Register(() => Should.NotThrow(() => reader1.Dispose()));
				var writer = rwLock.EnterWriterScopeAsync(cts.Token);
				var reader2 = rwLock.EnterReaderScopeAsync(CancellationToken.None);

				try
				{
					await writer;
					reader2.IsCompletedSuccessfully.ShouldBeFalse();
				}
				catch
				{
					reader2.IsCompletedSuccessfully.ShouldBeTrue();
				}
			}
		});
	}

	[Fact]
	public async Task EnterReaderScopeAsync_ReleaseQueuedWriter_WhenCancelledWhileAboutToRun()
	{
		await ParallelUtility.ForAsync(0, 10, async (_, _) =>
		{
			for (var i = 0; i < 100; i += 2)
			{
				var rwLock = AsyncReaderWriterLockFactory.Create(false, false);
				var writer1 = await rwLock.EnterWriterScopeAsync(CancellationToken.None);
				using var preCts = new CancellationTokenSource(30);
				using var cts = new CancellationTokenSource(i);
				preCts.Token.Register(() => Should.NotThrow(() => writer1.Dispose()));
				var reader1 = rwLock.EnterReaderScopeAsync(cts.Token);
				var writer2 = rwLock.EnterWriterScopeAsync(CancellationToken.None);
				await Should.NotThrowAsync(async () => await writer2);
				reader1.IsFaulted.ShouldBeFalse();
			}
		});
	}
}