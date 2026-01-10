using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

[DebuggerDisplay("Signaled={IsSignaled}, Waiters={WaiterQueue.Count}, Pooled={WaiterPool.Count}")]
public abstract class AsyncResetEvent : IDisposable
{
	private readonly bool _allowSynchronousContinuations;

	private protected readonly ConcurrentStack<Waiter> WaiterPool = new();
	private protected readonly ConcurrentQueue<Waiter> WaiterQueue = new();

	private protected int Signaled;

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

	public abstract void Set();
	public abstract ValueTask WaitAsync(CancellationToken cancellationToken = default);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(GetType().Name);
	}

	[DebuggerDisplay("HasResult={HasResult}, IsCancelled={_cancellationRegistration.Token.IsCancellationRequested}")]
	protected sealed class Waiter : IValueTaskSource
	{
		private readonly AsyncResetEvent _resetEvent;

		private CancellationTokenRegistration _cancellationRegistration;
		private ManualResetValueTaskSourceCore<bool> _core;
		private int _result;

		public Waiter(AsyncResetEvent resetEvent)
		{
			_resetEvent = resetEvent;
			_core.RunContinuationsAsynchronously = !resetEvent._allowSynchronousContinuations;
		}

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private bool HasResult => Volatile.Read(ref _result) == 1;

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

	protected Waiter Rent()
	{
		if (!WaiterPool.TryPop(out var waiter))
			waiter = new Waiter(this);
		return waiter;
	}

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