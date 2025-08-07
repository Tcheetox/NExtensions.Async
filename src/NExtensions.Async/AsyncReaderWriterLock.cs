using System.Diagnostics;
using System.Threading.Tasks.Sources;
using NExtensions.Async.Collections;

namespace NExtensions.Async;

/// <summary>
/// Provides an asynchronous reader-writer lock supporting multiple concurrent readers
/// and exclusive writers, with support for cancellation and pooling to reduce <see cref="Waiter"/> allocations.
/// </summary>
[DebuggerDisplay("Readers={_readerCount}/{_readerQueue.Count}, Writers={_writerActive}/{_writerQueue.Count}, Pooled={_waiterPool.Count}")]
public sealed class AsyncReaderWriterLock
{
	/// <summary>
	/// Release mode; reader or writer.
	/// </summary>
	public enum ReleaseMode
	{
		/// <summary>
		/// Indicates the lock was held and released by a writer.
		/// </summary>
		Writer,

		/// <summary>
		/// Indicates the lock was held and released by a reader.
		/// </summary>
		Reader
	}

	private readonly bool _allowSynchronousReaderContinuations;
	private readonly bool _allowSynchronousWriterContinuations;
	private readonly Deque<Waiter> _readerQueue = new(deepClear: false);

	private readonly object _sync = new();
	private readonly Stack<Waiter> _waiterPool = new();
	private readonly Deque<Waiter> _writerQueue = new(deepClear: false);
	private int _readerCount;
	private bool _writerActive;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncReaderWriterLock"/> class
	/// with the default behavior that disallows synchronous continuations
	/// for both readers and writers.
	/// </summary>
	public AsyncReaderWriterLock()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncReaderWriterLock"/> class
	/// with options to allow synchronous continuations for readers and writers.
	/// </summary>
	/// <param name="allowSynchronousReaderContinuations">
	/// If set to <c>true</c>, allows synchronous continuations for reader operations.
	/// </param>
	/// <param name="allowSynchronousWriterContinuations">
	/// If set to <c>true</c>, allows synchronous continuations for writer operations.
	/// </param>
	/// <remarks>Both options can significantly improve performance, but introduce additional risks such as reentrancy or stack dive.</remarks>
	public AsyncReaderWriterLock(bool allowSynchronousReaderContinuations, bool allowSynchronousWriterContinuations)
	{
		_allowSynchronousReaderContinuations = allowSynchronousReaderContinuations;
		_allowSynchronousWriterContinuations = allowSynchronousWriterContinuations;
	}

	/// <summary>
	/// Acquires a reader scope asynchronously. Multiple readers may acquire the scope simultaneously
	/// unless a writer is waiting or active. Can be canceled.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>A <see cref="ValueTask{Releaser}"/> that completes when the reader scope is acquired - must be disposed to release subsequent <see cref="Waiter"/>.</returns>
	/// <exception cref="OperationCanceledException">If the token gets canceled before the scope is acquired.</exception>
	public ValueTask<Releaser> EnterReaderScopeAsync(CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return ValueTask.FromCanceled<Releaser>(cancellationToken);

		lock (_sync)
		{
			if (!_writerActive && _writerQueue.Count == 0)
			{
				_readerCount++;
				return new ValueTask<Releaser>(new Releaser(this, ReleaseMode.Reader));
			}

			var waiter = Rent(ReleaseMode.Reader);
			_readerQueue.AddLast(waiter);
			if (cancellationToken.CanBeCanceled)
				waiter.SetCancellation(cancellationToken); // In case it's canceled, the callback will be run synchronously; hence the waiter must be in the queue!
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	/// <summary>
	/// Acquires a writer scope asynchronously. Only one writer can hold the scope,
	/// and it requires exclusive access (no active readers or writers). Can be canceled.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>A <see cref="ValueTask{Releaser}"/> that completes when the writer scope is acquired - must be disposed to release subsequent <see cref="Waiter"/>.</returns>
	/// <exception cref="OperationCanceledException">If the token gets canceled before the scope is acquired.</exception>
	public ValueTask<Releaser> EnterWriterScopeAsync(CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return ValueTask.FromCanceled<Releaser>(cancellationToken);

		lock (_sync)
		{
			if (!_writerActive && _readerCount == 0)
			{
				_writerActive = true;
				return new ValueTask<Releaser>(new Releaser(this, ReleaseMode.Writer));
			}

			var waiter = Rent(ReleaseMode.Writer);
			_writerQueue.AddLast(waiter);
			if (cancellationToken.CanBeCanceled) // Avoid binding useless tokens.
				waiter.SetCancellation(cancellationToken); // In case it's canceled, the callback will be run synchronously; hence the waiter must be in the queue!
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	private void Release(ReleaseMode mode, Waiter? cancelledWaiter = null)
	{
		Waiter? writerToWake = null;
		Waiter[]? readersToWake = null;

		lock (_sync)
		{
			switch (mode)
			{
				case ReleaseMode.Reader:
					if (cancelledWaiter is null)
					{
						Debug.Assert(_readerCount > 0, "This reader was active, it must be there!");
						_readerCount--;

						Debug.Assert(_readerCount >= 0);
					}
					else if (!_readerQueue.Remove(cancelledWaiter))
					{
						Debug.Assert(_readerCount > 0, "Because reader cancellation won the race against SetResult while in-flight.");
						_readerCount--;
					}

					break;
				case ReleaseMode.Writer:
					if (cancelledWaiter is null)
					{
						Debug.Assert(_writerActive);
						_writerActive = false;
					}
					else if (!_writerQueue.Remove(cancelledWaiter))
					{
						Debug.Assert(_writerActive, "Because writer cancellation won the race against SetResult while in-flight.");
						_writerActive = false;
					}

					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
			}

			if (_writerActive)
				return; // Early exit.

			// Attempt to get a writer.
			if (_readerCount == 0 && _writerQueue.TryRemoveFirst(out writerToWake))
			{
				_writerActive = true;
			}
			else if (_writerQueue.Count == 0 && _readerQueue.Count > 0)
			{
				Debug.Assert(!_writerActive, "Cannot have an active writer when about to wake readers.");
				readersToWake = new Waiter[_readerQueue.Count];
				_readerQueue.CopyTo(readersToWake);
				_readerQueue.Clear();
				_readerCount += readersToWake.Length; // Readers about to pick up (outside the lock).
			}
		}

		Debug.Assert(writerToWake == null || readersToWake == null, "Cannot wake both writer and readers simultaneously.");

		// Do not call SetResult in the lock to reduce contention, and protect it from synchronous continuations.
		if (writerToWake is not null)
		{
			writerToWake.TrySetResult(ReleaseMode.Writer);
			return;
		}

		if (readersToWake is null) return;
		foreach (var reader in readersToWake)
			reader.TrySetResult(ReleaseMode.Reader);
	}

	/// <summary>
	/// Represents a releasable struct handle to the reader or writer lock.
	/// Releasing the handle returns the lock and wakes up waiting operations if any.
	/// </summary>
	[DebuggerDisplay("Mode={_mode}")]
	public struct Releaser : IDisposable
	{
		private readonly AsyncReaderWriterLock _lock;
		private readonly ReleaseMode _mode;

		/// <summary>
		/// Initializes a new instance of the <see cref="Releaser"/> struct with the specified lock and release mode.
		/// This constructor is typically used internally by <see cref="AsyncReaderWriterLock"/> when a reader or writer scope is acquired.
		/// The created <see cref="Releaser"/> must be disposed to release the associated lock and wake waiting operations.
		/// </summary>
		/// <param name="rwLock">The <see cref="AsyncReaderWriterLock"/> instance associated with this <see cref="Releaser"/>.</param>
		/// <param name="mode">The <see cref="ReleaseMode"/> indicating whether this <see cref="Releaser"/> is for a reader or writer.</param>
		/// <remarks>
		/// This struct is not intended to be instantiated directly. Instead, it is returned by <see cref="EnterReaderScopeAsync"/> or <see cref="EnterWriterScopeAsync"/>.
		/// Disposing the <see cref="Releaser"/> releases the lock and allows waiting threads to proceed.
		/// </remarks>
		public Releaser(AsyncReaderWriterLock rwLock, ReleaseMode mode)
		{
			_lock = rwLock;
			_mode = mode;
		}

		private int _disposed = 0;

		/// <summary>
		/// Releases the associated lock once.
		/// </summary>
		/// <exception cref="ObjectDisposedException">If called multiple times.</exception>
		public void Dispose()
		{
			var disposed = Interlocked.Exchange(ref _disposed, 1);
			if (disposed == 1)
				throw new ObjectDisposedException(GetType().FullName);
			_lock.Release(_mode);
		}
	}

	[DebuggerDisplay("Mode={_mode}, HasResult={HasResult}, IsCancelled={_cancellationRegistration.Token.IsCancellationRequested}")]
	private sealed class Waiter : IValueTaskSource<Releaser>
	{
		private readonly AsyncReaderWriterLock _rwLock;

		private CancellationTokenRegistration _cancellationRegistration;
		private ManualResetValueTaskSourceCore<Releaser> _core;
		private ReleaseMode _mode;
		private int _result;

		public Waiter(AsyncReaderWriterLock rwLock)
		{
			_rwLock = rwLock;
		}

		public short Version => _core.Version;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private bool HasResult => Volatile.Read(ref _result) == 1;

		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _core.GetStatus(token);
		}

		public Releaser GetResult(short token)
		{
			var result = _core.GetResult(token);
			// If the target callback is currently executing, this method will wait until it completes.
			// Hence, we are safe to assume we cannot cancel the wrong target and return to the pool.
			_cancellationRegistration.Dispose();
			// Reset the necessary fields before returning to the pool.
			_core.Reset();
			Volatile.Write(ref _result, 0);
			_rwLock.Return(this);
			return result;
		}

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_core.OnCompleted(continuation, state, token, flags);
		}

		public void SetMode(ReleaseMode mode)
		{
			_mode = mode;
			// The safe default is asynchronous continuations.
			// It helps with reentrancy and prevents consumers from hijacking the thread.
			// For instance, the latter could prevent some readers from being notified as soon as the writer has finished.
			_core.RunContinuationsAsynchronously = mode switch
			{
				ReleaseMode.Writer => !_rwLock._allowSynchronousWriterContinuations,
				ReleaseMode.Reader => !_rwLock._allowSynchronousReaderContinuations,
				_ => true
			};
		}

		public void SetCancellation(CancellationToken cancellationToken)
		{
			_cancellationRegistration = cancellationToken.Register(static state => // This closure must be static to reduce allocation.
			{
				var self = (Waiter)state!;
				if (Interlocked.Exchange(ref self._result, 1) == 0)
				{
					self._rwLock.Release(self._mode, self); // Release before propagation.
					self._core.SetException(new OperationCanceledException(self._cancellationRegistration.Token));
				}
			}, this, true);
		}

		public void TrySetResult(ReleaseMode mode)
		{
			if (Interlocked.Exchange(ref _result, 1) == 0)
			{
				_core.SetResult(new Releaser(_rwLock, mode));
			}
		}
	}

	#region Pooling

	private Waiter Rent(ReleaseMode mode)
	{
		// This method must be called within the main lock.
		if (!_waiterPool.TryPop(out var waiter))
			waiter = new Waiter(this);
		waiter.SetMode(mode);
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