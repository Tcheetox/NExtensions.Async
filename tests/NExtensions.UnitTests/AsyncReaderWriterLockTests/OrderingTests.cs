using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncReaderWriterLockTests;

public class OrderingTests
{
	[Fact]
	public async Task QueuedWriters_ShouldBeServed_InFIFOOrder()
	{
		var rwLock = new AsyncReaderWriterLock();

		var writer1 = await rwLock.EnterWriterScopeAsync();
		// Queue two more writers, they should not complete yet
		var writer2Task = rwLock.EnterWriterScopeAsync();
		var writer3Task = rwLock.EnterWriterScopeAsync();

		writer2Task.IsCompleted.ShouldBeFalse();
		writer3Task.IsCompleted.ShouldBeFalse();

		// Release first writer, the second writer should acquire
		writer1.Dispose();
		writer2Task.IsCompletedSuccessfully.ShouldBeTrue();

		var writer2 = await writer2Task;
		writer3Task.IsCompleted.ShouldBeFalse();

		// Release second writer, the third writer should acquire
		writer2.Dispose();
		writer3Task.IsCompletedSuccessfully.ShouldBeTrue();
		var writer3 = await writer3Task;
		writer3.Dispose();
	}

	[Fact]
	public async Task QueuedReaders_ShouldBeServedTogether_WhenNoWritersWaiting()
	{
		var rwLock = new AsyncReaderWriterLock();

		var reader1 = await rwLock.EnterReaderScopeAsync();
		var reader2Task = rwLock.EnterReaderScopeAsync();
		var reader3Task = rwLock.EnterReaderScopeAsync();

		// Assert that these readers have not yet acquired the lock
		reader2Task.IsCompletedSuccessfully.ShouldBeTrue();
		reader3Task.IsCompletedSuccessfully.ShouldBeTrue();

		// Release first reader - this should release all queued readers simultaneously
		reader1.Dispose();
		(await reader2Task).Dispose();
		(await reader3Task).Dispose();
	}

	[Fact]
	public async Task QueuedReaders_ShouldBeServedTogether_AfterWriterReleases()
	{
		var rwLock = new AsyncReaderWriterLock();

		var writer = await rwLock.EnterWriterScopeAsync();
		var reader1Task = rwLock.EnterReaderScopeAsync();
		var reader2Task = rwLock.EnterReaderScopeAsync();

		// Both readers should be waiting (not completed)
		reader1Task.IsCompleted.ShouldBeFalse();
		reader2Task.IsCompleted.ShouldBeFalse();

		// Release writer, readers should now acquire lock simultaneously
		writer.Dispose();

		reader1Task.IsCompletedSuccessfully.ShouldBeTrue();
		reader2Task.IsCompletedSuccessfully.ShouldBeTrue();

		(await reader1Task).Dispose();
		(await reader2Task).Dispose();
	}

	[Fact]
	public async Task QueuedReader_ShouldBeSkipped_WhenCancelled()
	{
		var rwLock = new AsyncReaderWriterLock();
		var writer = await rwLock.EnterWriterScopeAsync();

		// Create a cancellation token source and cancel it immediately
		using var cts = new CancellationTokenSource(TimeSpan.Zero);
		var cancelledReaderTask = rwLock.EnterReaderScopeAsync(cts.Token);
		cancelledReaderTask.IsCanceled.ShouldBeTrue();

		// Now queue a valid reader that should wait
		var validReaderTask = rwLock.EnterReaderScopeAsync(CancellationToken.None);
		writer.Dispose();
		validReaderTask.IsCompletedSuccessfully.ShouldBeTrue();
		(await validReaderTask).Dispose();
	}

	[Fact]
	public async Task QueuedWriter_ShouldBeSkipped_WhenCancelled()
	{
		var rwLock = new AsyncReaderWriterLock();

		var writer1 = await rwLock.EnterWriterScopeAsync();
		using var cts = new CancellationTokenSource(TimeSpan.Zero);

		// Enqueue a writer with canceled token (should never acquire)
		var cancelledWriterTask = rwLock.EnterWriterScopeAsync(cts.Token);
		cancelledWriterTask.IsCanceled.ShouldBeTrue();

		var writer2Task = rwLock.EnterWriterScopeAsync(CancellationToken.None);
		writer1.Dispose();
		writer2Task.IsCompletedSuccessfully.ShouldBeTrue();
		(await writer2Task).Dispose();
	}

	[Fact]
	public async Task QueuedWriter_ShouldBeServed_BeforeSubsequentReaders()
	{
		var rwLock = new AsyncReaderWriterLock();

		// Acquire initial reader lock
		var initialReader = await rwLock.EnterReaderScopeAsync();

		// Queue a writer, then queue two readers
		var writerTask = rwLock.EnterWriterScopeAsync();
		var reader1Task = rwLock.EnterReaderScopeAsync();
		var reader2Task = rwLock.EnterReaderScopeAsync();

		// None of the queued tasks should be completed yet
		writerTask.IsCompleted.ShouldBeFalse();
		reader1Task.IsCompleted.ShouldBeFalse();
		reader2Task.IsCompleted.ShouldBeFalse();

		// Release the initial reader to unblock the queue, hence writer should now be allowed to proceed, but not readers
		initialReader.Dispose();
		writerTask.IsCompletedSuccessfully.ShouldBeTrue();
		reader1Task.IsCompleted.ShouldBeFalse();
		reader2Task.IsCompleted.ShouldBeFalse();

		// Complete writer and allow readers to acquire lock together
		var writer = await writerTask;
		writer.Dispose();

		// Now both readers should be allowed to proceed
		reader1Task.IsCompletedSuccessfully.ShouldBeTrue();
		reader2Task.IsCompletedSuccessfully.ShouldBeTrue();

		(await reader1Task).Dispose();
		(await reader2Task).Dispose();
	}
}