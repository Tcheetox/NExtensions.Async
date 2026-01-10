namespace NExtensions.Async;

public class AsyncManualResetEvent : AsyncResetEvent
{
	public AsyncManualResetEvent(bool initialState, bool allowSynchronousContinuations = false)
		: base(initialState, allowSynchronousContinuations)
	{
	}

	public override void Set()
	{
		ThrowIfDisposed();

		Interlocked.Exchange(ref Signaled, 1); // Signals, then release waiters (if any).

		while (WaiterQueue.TryDequeue(out var waiter))
		{
			waiter.TrySetResult(); // Some might be canceled.
		}
	}

	public override ValueTask WaitAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		if (cancellationToken.IsCancellationRequested)
			return ValueTask.FromCanceled(cancellationToken);

		var signaled = Volatile.Read(ref Signaled) == 1;
		if (signaled)
			return ValueTask.CompletedTask;

		var waiter = Rent();
		WaiterQueue.Enqueue(waiter);

		// Check if we have missed the signal while enqueuing.
		signaled = Volatile.Read(ref Signaled) == 1;
		if (signaled && waiter.TrySetResult())
			return new ValueTask(waiter, waiter.Version); // Cancellation is no longer possible, early return.

		if (cancellationToken.CanBeCanceled)
			waiter.SetCancellation(cancellationToken);

		return new ValueTask(waiter, waiter.Version);
	}
}