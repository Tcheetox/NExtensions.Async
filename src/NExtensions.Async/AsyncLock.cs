using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks.Sources;
using NExtensions.Async.Collections;

namespace NExtensions.Async;

/// <summary>
/// Represents an asynchronous mutual-exclusion lock. Allows asynchronous code to enter a critical section exclusively.
/// </summary>
[DebuggerDisplay("Taken={_active}, Waiters={_waiterQueue.Count}, Pooled={_waiterPool.Count}")]
public sealed class AsyncLock
{
	private readonly bool _allowSynchronousContinuations;
	private readonly ConcurrentStack<Waiter> _waiterPool = new();
	private readonly Deque<Waiter> _waiterQueue = new(deepClear: false);

#if NET9_0_OR_GREATER
	private readonly Lock _sync = new();
#else
	private readonly object _sync = new();
#endif

	private bool _active;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncLock"/> class
	/// with the default behavior that disallows synchronous continuations.
	/// </summary>
	public AsyncLock()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncLock"/> class,
	/// optionally allowing continuations to run synchronously when the lock is released.
	/// </summary>
	/// <param name="allowSynchronousContinuations">
	/// If <c>true</c>, continuations after acquiring the lock may run synchronously.
	/// If <c>false</c>, continuations will always run asynchronously.
	/// </param>
	/// <remarks>Synchronous continuations can significantly improve performance, but introduce additional risks such as reentrancy or stack dive.</remarks>
	public AsyncLock(bool allowSynchronousContinuations)
	{
		_allowSynchronousContinuations = allowSynchronousContinuations;
	}

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
			_waiterQueue.AddLast(waiter);
			if (cancellationToken.CanBeCanceled)
				waiter.SetCancellation(cancellationToken); // In case it's canceled, the callback will be run synchronously; hence the waiter must be in the queue! 
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	private void Release(Waiter? cancelledWaiter = null)
	{
		Waiter? waiterToWake;

		lock (_sync)
		{
			Debug.Assert(_active, "Release called only when the lock is currently held, even if cancelled.");
			// Either the waiter was not canceled (thus attempt to wake the next waiter in line), or it was canceled.
			// In the latter case, if the canceled waiter is in the queue and can be removed, it means there's still a legit lock holder.
			// Otherwise, it means the waiter was scheduled to acquire the lock but the SetException won the race, hence wake up the next waiter.
			if (cancelledWaiter is not null && _waiterQueue.Remove(cancelledWaiter))
				return;
			_active = _waiterQueue.TryRemoveFirst(out waiterToWake);
		}

		Debug.Assert(waiterToWake is not null == _active, "Invalid state: _active must match waiter presence.");
		waiterToWake?.TrySetResult();
	}

	/// <summary>
	/// A struct returned by <see cref="EnterScopeAsync"/> that releases the lock when disposed.
	/// </summary>
	public struct Releaser : IDisposable
	{
		private readonly AsyncLock _asyncLock;

		/// <summary>
		/// Initializes a new instance of the <see cref="Releaser"/> struct associated with the specified <see cref="AsyncLock"/>.
		/// This constructor is used internally by <see cref="AsyncLock"/> when a thread successfully acquires the lock.
		/// The created <see cref="Releaser"/> must be disposed to release the lock and allow other threads to proceed.
		/// </summary>
		/// <param name="asyncLock">The <see cref="AsyncLock"/> instance that this <see cref="Releaser"/> will manage.</param>
		/// <remarks>
		/// This struct is not intended for direct instantiation. It is returned by <see cref="EnterScopeAsync"/> when the lock is acquired.
		/// Disposing the <see cref="Releaser"/> releases the lock. Failure to dispose it will prevent other threads from acquiring the lock.
		/// </remarks>
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

	[DebuggerDisplay("HasResult={HasResult}, IsCancelled={_cancellationRegistration.Token.IsCancellationRequested}")]
	private sealed class Waiter : IValueTaskSource<Releaser>
	{
		private readonly AsyncLock _asyncLock;

		private CancellationTokenRegistration _cancellationRegistration;
		private ManualResetValueTaskSourceCore<Releaser> _core;
		private int _result;

		public Waiter(AsyncLock asyncLock)
		{
			_asyncLock = asyncLock;
			_core.RunContinuationsAsynchronously = !asyncLock._allowSynchronousContinuations;
		}

		public short Version => _core.Version;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private bool HasResult => Volatile.Read(ref _result) == 1;

		public Releaser GetResult(short token)
		{
			var result = _core.GetResult(token);
			_cancellationRegistration.Dispose();
			// Reset the necessary fields before returning to the pool.
			_core.Reset();
			Volatile.Write(ref _result, 0);
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

		public void TrySetResult()
		{
			if (Interlocked.Exchange(ref _result, 1) == 0)
			{
				_core.SetResult(new Releaser(_asyncLock));
			}
		}

		public void SetCancellation(CancellationToken cancellationToken)
		{
			_cancellationRegistration = cancellationToken.Register(static state => // This closure must be static to reduce allocation.
			{
				var self = (Waiter)state!;
				if (Interlocked.Exchange(ref self._result, 1) == 0)
				{
					self._asyncLock.Release(self); // Release before propagation.
					self._core.SetException(new OperationCanceledException(self._cancellationRegistration.Token));
				}
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
}