using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

/// <summary>
/// Provides an abstract base class for asynchronous reset events.
/// </summary>
[DebuggerDisplay("Signaled={IsSignaled}, Waiters={WaiterQueue.Count}, Pooled={WaiterPool.Count}")]
public abstract class AsyncResetEvent : IDisposable
{
	private readonly bool _allowSynchronousContinuations;

	private protected readonly ConcurrentStack<Waiter> WaiterPool = new();
	private protected readonly ConcurrentQueue<Waiter> WaiterQueue = new();

	private protected int Signaled;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncResetEvent"/> class.
	/// </summary>
	/// <param name="initialState">Sets the initial state of the event; <c>true</c> for signaled, <c>false</c> for non-signaled.</param>
	/// <param name="allowSynchronousContinuations">
	/// If <c>true</c>, continuations after waiting on the event may run synchronously.
	/// If <c>false</c>, continuations will always run asynchronously.
	/// </param>
	/// <remarks>Synchronous continuations can significantly improve performance but introduce additional risks such as reentrancy or stack dive.</remarks>
	protected AsyncResetEvent(bool initialState, bool allowSynchronousContinuations = false)
	{
		Signaled = initialState ? 1 : 0;
		_allowSynchronousContinuations = allowSynchronousContinuations;
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private protected bool IsSignaled => Volatile.Read(ref Signaled) == 1;

	/// <summary>
	/// Resets the event to a non-signaled state, preventing tasks from proceeding until the event is signaled again.
	/// </summary>
	/// <exception cref="ObjectDisposedException">
	/// Thrown if the instance has been disposed.
	/// </exception>
	public void Reset()
	{
		ThrowIfDisposed();
		Interlocked.Exchange(ref Signaled, 0);
	}

	/// <summary>
	/// When overridden in a derived class, signals the event, allowing one or more waiting tasks to proceed.
	/// </summary>
	public abstract void Set();

	/// <summary>
	/// When overridden in a derived class, waits asynchronously for the event to be signaled.
	/// </summary>
	/// <param name="cancellationToken">
	/// A <see cref="CancellationToken"/> to observe while waiting.
	/// </param>
	/// <returns>
	/// A <see cref="ValueTask"/> that completes when the event is signaled or the cancellation token is canceled.
	/// </returns>
	public abstract ValueTask WaitAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Throws an <see cref="ObjectDisposedException"/> if the instance has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException">If disposed.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(GetType().Name);
	}

	/// <summary>
	/// Represents a waiter for the asynchronous reset event.
	/// </summary>
	[DebuggerDisplay("HasResult={HasResult}, IsCancelled={_cancellationRegistration.Token.IsCancellationRequested}")]
	protected sealed class Waiter : IValueTaskSource
	{
		private readonly AsyncResetEvent _resetEvent;

		private CancellationTokenRegistration _cancellationRegistration;
		private ManualResetValueTaskSourceCore<bool> _core;
		private int _result;

		/// <summary>
		/// Initializes a new instance of the <see cref="Waiter"/> class.
		/// </summary>
		/// <param name="resetEvent">The <see cref="AsyncResetEvent"/> instance associated with this waiter.</param>
		public Waiter(AsyncResetEvent resetEvent)
		{
			_resetEvent = resetEvent;
			_core.RunContinuationsAsynchronously = !resetEvent._allowSynchronousContinuations;
		}

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private bool HasResult => Volatile.Read(ref _result) == 1;

		/// <summary>
		/// Gets the operation version of the <see cref="ManualResetValueTaskSourceCore{TResult}"/> instance associated with this waiter.
		/// </summary>
		public short Version => _core.Version;

		/// <inheritdoc />
		public void GetResult(short token)
		{
			_ = _core.GetResult(token); // Will throw if the task was canceled.
			_cancellationRegistration.Dispose();
			_core.Reset();
			Volatile.Write(ref _result, 0);
			_resetEvent.Return(this);
		}

		/// <inheritdoc />
		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _core.GetStatus(token);
		}

		/// <inheritdoc />
		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_core.OnCompleted(continuation, state, token, flags);
		}

		/// <summary>
		/// Attempts to set the result for the waiter.
		/// </summary>
		/// <returns><c>true</c> if the result was successfully set; <c>false</c> if the waiter was already completed or canceled.</returns>
		public bool TrySetResult()
		{
			if (Interlocked.Exchange(ref _result, 1) == 0)
			{
				_core.SetResult(true);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Sets the cancellation token for the waiter.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
		public void SetCancellation(CancellationToken cancellationToken)
		{
			_cancellationRegistration = cancellationToken.Register(static state =>
			{
				var self = Unsafe.As<Waiter>(state)!;
				if (Interlocked.Exchange(ref self._result, 1) == 0)
					self._core.SetException(new OperationCanceledException(self._cancellationRegistration.Token));
			}, this, false);
		}
	}

	#region Pooling

	/// <summary>
	/// Rents a <see cref="Waiter"/> instance from the pool or creates a new one if the pool is empty.
	/// </summary>
	/// <returns>A <see cref="Waiter"/> instance.</returns>
	protected Waiter Rent()
	{
		if (!WaiterPool.TryPop(out var waiter))
			waiter = new Waiter(this);
		return waiter;
	}

	/// <summary>
	/// Returns a <see cref="Waiter"/> instance to the pool for reuse.
	/// </summary>
	/// <param name="waiter">The <see cref="Waiter"/> instance to return.</param>
	protected void Return(Waiter waiter)
	{
		WaiterPool.Push(waiter);
	}

	#endregion

	#region IDisposable

	private bool _disposed;

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		// We decide to match the behavior of AutoResetEvent or SemaphoreSlim by having the waiters hanging.
		WaiterQueue.Clear();
		WaiterPool.Clear();
		GC.SuppressFinalize(this);
	}

	#endregion
}