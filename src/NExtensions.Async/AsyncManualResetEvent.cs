namespace NExtensions.Async;

/// <summary>
/// Represents an asynchronous manual-reset event.
/// When signaled, it remains signaled until it is manually reset, allowing all waiting tasks to proceed.
/// </summary>
public class AsyncManualResetEvent : AsyncResetEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncManualResetEvent"/> class with a value indicating 
	/// whether to set the initial state to signaled and optionally allowing synchronous continuations.
	/// </summary>
	/// <inheritdoc/>
	public AsyncManualResetEvent(bool initialState, bool allowSynchronousContinuations = false)
		: base(initialState, allowSynchronousContinuations)
	{
	}

	/// <summary>
	/// Signals the event, allowing all waiting tasks to proceed. The event remains signaled until <see cref="AsyncResetEvent.Reset"/> is called.
	/// </summary>
	/// <exception cref="ObjectDisposedException">
	/// Thrown if the instance has been disposed.
	/// </exception>
	public override void Set()
	{
		ThrowIfDisposed();

		Interlocked.Exchange(ref Signaled, 1); // Signals, then release waiters (if any).

		while (WaiterQueue.TryDequeue(out var waiter))
		{
			waiter.TrySetResult(); // Some might be canceled.
		}
	}

	/// <summary>
	/// Waits asynchronously for the event to be signaled. If the event is already signaled, the method returns immediately.
	/// Otherwise, it will wait until the event is signaled or if the provided cancellation token is canceled.
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