using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

[DebuggerDisplay("Signaled={Signaled}, Waiters={_waiterQueue.Count}, Pooled={_waiterPool.Count}")]
public class AsyncAutoResetEvent : IDisposable
{
	private readonly bool _allowSynchronousContinuations;
	private readonly ConcurrentStack<Waiter> _waiterPool = new();
	private readonly ConcurrentQueue<Waiter> _waiterQueue = new();
	
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private bool Signaled => Volatile.Read(ref _signaled) == 1;
	
	private int _signaled;
	
	public AsyncAutoResetEvent(bool initialState, bool allowSynchronousContinuations = false)
	{
		_signaled = initialState ? 1 : 0;
		_allowSynchronousContinuations = allowSynchronousContinuations;
	}

	/// <summary>
	/// Resets the event to a non-signaled state, preventing tasks from proceeding until the event is signaled again.
	/// </summary>
	/// <exception cref="ObjectDisposedException">
	/// Thrown if the instance has been disposed.
	/// </exception>
	public void Reset()
	{
		ThrowIfDisposed();
		Interlocked.Exchange(ref _signaled, 0);
	}
	
	/// <summary>
	/// Signals the event, allowing one waiting task to proceed. If no tasks are waiting,
	/// sets the event to a signaled state.
	/// </summary>
	/// <exception cref="ObjectDisposedException">
	/// Thrown if the instance has been disposed.
	/// </exception>
	public void Set()
	{
		ThrowIfDisposed();
		
		while (_waiterQueue.TryDequeue(out var waiter))
		{
			if (waiter.TrySetResult()) // If we fail to set the result, it means the waiter was canceled.
				return;
		}
		
		Interlocked.Exchange(ref _signaled, 1);
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
	public ValueTask WaitAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		if (cancellationToken.IsCancellationRequested)
			return ValueTask.FromCanceled(cancellationToken);

		if (Interlocked.CompareExchange(ref _signaled, 0, 1) == 1)
			return ValueTask.CompletedTask;

		var waiter = Rent();
		_waiterQueue.Enqueue(waiter);
		
		// Try to consume the signal again - if we get it, complete ourselves unless we are late to the party.
		if (Interlocked.CompareExchange(ref _signaled, 0, 1) == 1 && !waiter.TrySetResult())
		{
			// We are already consumed by another thread calling Set(), restore the signal by calling Set() ourselves.
			// At this stage it could not have been a cancellation because we haven't bound the callback yet (see below).
			Set();
		}
		if (cancellationToken.CanBeCanceled)
			waiter.SetCancellation(cancellationToken);

		return new ValueTask(waiter, waiter.Version);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(AsyncAutoResetEvent));
	}

	[DebuggerDisplay("HasResult={HasResult}, IsCancelled={_cancellationRegistration.Token.IsCancellationRequested}")]
	private sealed class Waiter : IValueTaskSource
	{
		private readonly AsyncAutoResetEvent _resetEvent;

		private CancellationTokenRegistration _cancellationRegistration;
		private ManualResetValueTaskSourceCore<bool> _core;
		private int _result;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private bool HasResult => Volatile.Read(ref _result) == 1;
		
		public Waiter(AsyncAutoResetEvent resetEvent)
		{
			_resetEvent = resetEvent;
			_core.RunContinuationsAsynchronously = !resetEvent._allowSynchronousContinuations;
		}
		
		public short Version => _core.Version;

		public void GetResult(short token)
		{
			_ = _core.GetResult(token); // Will throw if the task was canceled.
			_cancellationRegistration.Dispose();
			_core.Reset();
			Volatile.Write(ref _result, 0);
			_resetEvent.Return(this);
		}

		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _core.GetStatus(token);
		}

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_core.OnCompleted(continuation, state, token, flags);
		}

		public bool TrySetResult()
		{
			if (Interlocked.Exchange(ref _result, 1) == 0)
			{
				_core.SetResult(true);
				return true;
			}

			return false;
		}

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
	private Waiter Rent()
	{
		if (!_waiterPool.TryPop(out var waiter))
			waiter = new Waiter(this);
		return waiter;
	}

	private void Return(Waiter waiter)
	{
		_waiterPool.Push(waiter);
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
		_waiterQueue.Clear();
		_waiterPool.Clear();
		GC.SuppressFinalize(this);
	}
	#endregion
}