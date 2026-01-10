namespace NExtensions.Async;

public class AsyncAutoResetEvent : AsyncResetEvent
{
	public AsyncAutoResetEvent(bool initialState, bool allowSynchronousContinuations = false)
		: base(initialState, allowSynchronousContinuations)
	{
	}

	/// <summary>
	/// Signals the event, allowing one waiting task to proceed. If no tasks are waiting,
	/// sets the event to a signaled state.
	/// </summary>
	/// <exception cref="ObjectDisposedException">
	/// Thrown if the instance has been disposed.
	/// </exception>
	public override void Set()
	{
		ThrowIfDisposed();

		while (WaiterQueue.TryDequeue(out var waiter))
		{
			if (waiter.TrySetResult()) // If we fail to set the result, it means the waiter was canceled.
				return;
		}

		Interlocked.Exchange(ref Signaled, 1);
	}


	/// <summary>
	/// Waits asynchronously for the event to be signaled. If the event is already signaled, the method returns immediately.
	/// Otherwise, it will wait until the event is signaled or the specified cancellation token is canceled.
	/// </summary>
	/// <param name="cancellationToken">
	/// A <see cref="CancellationToken"/> to observe while waiting. If the token is canceled, the wait is aborted, and a <see cref="ValueTask"/> is returned in a canceled state.
	/// </param>
	/// <returns>
	/// A <see cref="ValueTask"/> that completes when the event is signaled or the cancellation token is canceled.
	/// </returns>
	/// <exception cref="ObjectDisposedException">
	/// Thrown if the instance has been disposed.
	/// </exception>
	public override ValueTask WaitAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		if (cancellationToken.IsCancellationRequested)
			return ValueTask.FromCanceled(cancellationToken);

		// Fast path.
		if (Interlocked.Exchange(ref Signaled, 0) == 1)
			return ValueTask.CompletedTask;

		var waiter = Rent();
		WaiterQueue.Enqueue(waiter);

		// Try to consume the signal again - if we get it, complete ourselves unless we are late to the party.
		if (Interlocked.Exchange(ref Signaled, 0) == 1)
		{
			if (waiter.TrySetResult())
				goto end; // No cancellation possible, early return.

			// We are already consumed by another thread calling Set(), manually set another waiter or restore the proper signal.
			// Note that at this stage it could not have been a cancellation because we haven't bound the callback yet.
			while (WaiterQueue.TryDequeue(out var otherWaiter))
			{
				if (otherWaiter.TrySetResult())
					goto end; // In this case the signal is in the proper state as far as we are concerned.
			}

			Interlocked.Exchange(ref Signaled, 1);
		}

		if (cancellationToken.CanBeCanceled)
			waiter.SetCancellation(cancellationToken);

		end:
		return new ValueTask(waiter, waiter.Version);
	}
}