using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

/// <summary>
/// Represents an asynchronous mutual-exclusion lock. Allows asynchronous code to enter a critical section exclusively.
/// </summary>
[DebuggerDisplay("Taken={_active}, Waiters={_waiterQueue.Count}, Pooled={_waiterPool.Count}")]
public sealed class AsyncLock
{
	private readonly object _sync = new();
	private readonly Stack<Waiter> _waiterPool = new();
	private readonly Queue<Waiter> _waiterQueue = new();

	private bool _active;

	/// <summary>
	/// Asynchronously waits to enter the lock and returns a <see cref="Releaser"/> struct that releases the lock when disposed.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the wait for the lock.</param>
	/// <returns>
	/// A <see cref="ValueTask{TResult}"/> that completes when the lock is entered.
	/// The result contains a <see cref="Releaser"/> that must be disposed to release the lock.
	/// </returns>
	/// <exception cref="OperationCanceledException">
	/// Thrown if the provided <paramref name="cancellationToken"/> is canceled before or during the wait.
	/// </exception>
	public ValueTask<Releaser> EnterScopeAsync(CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return ValueTask.FromCanceled<Releaser>(cancellationToken);

		lock (_sync)
		{
			if (!_active)
			{
				_active = true; // Fast path.
				return new ValueTask<Releaser>(new Releaser(this));
			}

			var waiter = Rent();
			if (cancellationToken.CanBeCanceled)
				waiter.SetCancellation(cancellationToken);
			_waiterQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	private void Release(bool cancelled = false)
	{
		Waiter? waiterToWake;

		lock (_sync)
		{
			if (cancelled)
			{
				if (_active)
					return; // Early exit.
			}
			else
			{
				Debug.Assert(_active);
				_active = false;
			}

			Debug.Assert(!_active);

			while (_waiterQueue.TryDequeue(out waiterToWake))
			{
				if (!waiterToWake.TryReserve())
					continue;
				_active = true; // Adjust new state.
				break;
			}
		}

		waiterToWake?.SetResult(new Releaser(this));
	}

	/// <summary>
	/// A struct returned by <see cref="EnterScopeAsync"/> that releases the lock when disposed.
	/// </summary>
	public struct Releaser : IDisposable
	{
		private readonly AsyncLock _asyncLock;

		public Releaser(AsyncLock asyncLock)
		{
			_asyncLock = asyncLock;
		}

		private int _disposed = 0;

		/// <summary>
		/// Releases the lock held by this instance.
		/// </summary>
		/// <exception cref="ObjectDisposedException">
		/// Thrown if this <see cref="Releaser"/> is disposed multiple times.
		/// </exception>
		public void Dispose()
		{
			var disposed = Interlocked.Exchange(ref _disposed, 1);
			if (disposed == 1)
				throw new ObjectDisposedException(GetType().FullName);
			_asyncLock.Release();
		}
	}

	[DebuggerDisplay("Reserved={Reserved}, IsCancelled={_cancellationRegistration.Token.IsCancellationRequested}")]
	private sealed class Waiter : IValueTaskSource<Releaser>
	{
		private readonly AsyncLock _asyncLock;

		private CancellationTokenRegistration _cancellationRegistration;
		private ManualResetValueTaskSourceCore<Releaser> _core; // Continuations are allowed to run synchronously by default, as opposed to our AsyncReaderWriterLock.

		private int _reserved;

		public Waiter(AsyncLock asyncLock)
		{
			_asyncLock = asyncLock;
		}

		public short Version => _core.Version;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private bool Reserved => Volatile.Read(ref _reserved) != 0;

		public Releaser GetResult(short token)
		{
			var result = _core.GetResult(token);
			// Reset the necessary fields before returning to the pool.
			_cancellationRegistration.Dispose();
			_core.Reset();
			_reserved = 0;
			_asyncLock.Return(this);
			return result;
		}

		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _core.GetStatus(token);
		}

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_core.OnCompleted(continuation, state, token, flags);
		}

		public void SetResult(in Releaser releaser)
		{
			Debug.Assert(Reserved);
			_core.SetResult(releaser);
		}

		public bool TryReserve()
		{
			return Interlocked.Exchange(ref _reserved, 1) == 0;
		}

		public void SetCancellation(CancellationToken cancellationToken)
		{
			_cancellationRegistration = cancellationToken.Register(static state => // This closure must be static to reduce allocation.
			{
				var self = (Waiter)state!;
				if (self.TryReserve())
				{
					self._asyncLock.Release(true); // Release before propagation.
					self._core.SetException(new OperationCanceledException(self._cancellationRegistration.Token));
				}
			}, this);
		}
	}

	#region Pooling

	private Waiter Rent()
	{
		// This method must be called within the main lock.
		if (!_waiterPool.TryPop(out var waiter))
			waiter = new Waiter(this);
		return waiter;
	}

	private void Return(Waiter waiter)
	{
		lock (_sync)
		{
			_waiterPool.Push(waiter);
		}
	}

	#endregion
}