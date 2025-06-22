using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

/// <summary>
/// Provides an asynchronous reader-writer lock supporting multiple concurrent readers
/// and exclusive writers, with support for cancellation and pooling to reduce <see cref="Waiter"/> allocations.
/// </summary>
[DebuggerDisplay("Readers={_readerCount}/{_readerQueue.Count}, Writers={_writerActive}/{_writerQueue.Count}, Pool={_waiterPool.Count}")]
public sealed class AsyncReaderWriterLock
{
	/// <summary>
	/// Represents the mode used during release: Reader, Writer, or Cancelled.
	/// </summary>
	public enum ReleaseMode
	{
		/// <summary>
		/// Indicates that the lock was released due to cancellation.
		/// </summary>
		Cancelled,

		/// <summary>
		/// Indicates the lock was held and released by a writer.
		/// </summary>
		Writer,

		/// <summary>
		/// Indicates the lock was held and released by a reader.
		/// </summary>
		Reader
	}

	private readonly Queue<Waiter> _readerQueue = new();
	private readonly object _sync = new();
	private readonly Stack<Waiter> _waiterPool = new();
	private readonly Queue<Waiter> _writerQueue = new();
	private int _readerCount;
	private bool _writerActive;

	/// <summary>
	/// Acquires a reader lock asynchronously. Multiple readers may acquire the lock simultaneously
	/// unless a writer is waiting or active. Can be canceled.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>A <see cref="ValueTask{Releaser}"/> that completes when the reader lock is acquired - must be disposed to release subsequent <see cref="Waiter"/>.</returns>
	/// <exception cref="OperationCanceledException">If the token gets canceled before the lock is acquired.</exception>
	public ValueTask<Releaser> ReaderLockAsync(CancellationToken cancellationToken = default)
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

			var waiter = Rent();
			if (cancellationToken.CanBeCanceled)
				waiter.SetCancellation(cancellationToken);
			_readerQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	/// <summary>
	/// Acquires a writer lock asynchronously. Only one writer can hold the lock,
	/// and it requires exclusive access (no active readers or writers). Can be canceled.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>A <see cref="ValueTask{Releaser}"/> that completes when the writer lock is acquired - must be disposed to release subsequent <see cref="Waiter"/>.</returns>
	/// <exception cref="OperationCanceledException">If the token gets canceled before the lock is acquired.</exception>
	public ValueTask<Releaser> WriterLockAsync(CancellationToken cancellationToken = default)
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

			var waiter = Rent();
			if (cancellationToken.CanBeCanceled) // Avoid binding useless tokens.
				waiter.SetCancellation(cancellationToken);
			_writerQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	private void Release(ReleaseMode mode)
	{
		Waiter? writerToWake = null;
		List<Waiter>? readersToWake = null;

		lock (_sync)
		{
			switch (mode)
			{
				case ReleaseMode.Reader:
					_readerCount--;
					Debug.Assert(_readerCount >= 0);
					break;
				case ReleaseMode.Writer:
					Debug.Assert(_writerActive);
					_writerActive = false;
					break;
				case ReleaseMode.Cancelled:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
			}

			if (_writerActive)
				return; // Early exit.

			// Attempt to get a writer.
			if (_readerCount == 0)
			{
				while (_writerQueue.TryDequeue(out writerToWake))
				{
					if (writerToWake.HasResult)
						continue;
					_writerActive = true;
					break;
				}
			}

			// Check for readers if no writers.
			if (writerToWake is null && _readerQueue.Count > 0)
			{
				Debug.Assert(!_writerActive, "Cannot have an active writer when about to wake readers.");
				readersToWake = new List<Waiter>(_readerQueue.Count);
				while (_readerQueue.TryDequeue(out var waiter) && !waiter.HasResult) // Dismiss waiters which have canceled.
					readersToWake.Add(waiter);
				_readerCount += readersToWake.Count; // Readers about to pick up (outside the lock).
			}
		}

		Debug.Assert(writerToWake == null || readersToWake == null, "Cannot wake both writer and readers simultaneously.");

		// Do not call SetResult in the lock to reduce potential contentions.
		writerToWake?.SetResult(new Releaser(this, ReleaseMode.Writer));
		if (readersToWake == null) return;
		foreach (var reader in readersToWake)
			reader.SetResult(new Releaser(this, ReleaseMode.Reader));
	}

	/// <summary>
	/// Represents a releasable struct handle to the reader or writer lock.
	/// Releasing the handle returns the lock and wakes up waiting operations.
	/// </summary>
	[DebuggerDisplay("Mode={_mode}")]
	public struct Releaser : IDisposable
	{
		private readonly AsyncReaderWriterLock _lock;
		private readonly ReleaseMode _mode;

		public Releaser(AsyncReaderWriterLock rwLock, ReleaseMode mode)
		{
			_lock = rwLock;
			_mode = mode;
		}

		private int _disposed;

		/// <summary>
		/// Releases the lock - must only be called once.
		/// </summary>
		/// <exception cref="ObjectDisposedException">If called multiple times.</exception>
		public void Dispose()
		{
			var disposed = Interlocked.Exchange(ref _disposed, 1);
			ObjectDisposedException.ThrowIf(disposed == 1, this);
			_lock.Release(_mode);
		}
	}

	[DebuggerDisplay("HasResult={HasResult}, IsCancelled={_cancellationRegistration.Token.IsCancellationRequested}")]
	private sealed class Waiter : IValueTaskSource<Releaser>
	{
		private readonly AsyncReaderWriterLock _rwLock;
		private CancellationTokenRegistration _cancellationRegistration;

		private ManualResetValueTaskSourceCore<Releaser> _core = new()
		{
			// Ensures no reentrancy and prevents the consumers to hijack the thread.
			RunContinuationsAsynchronously = true
		};

		private int _result;

		public Waiter(AsyncReaderWriterLock rwLock)
		{
			_rwLock = rwLock;
		}

		public short Version => _core.Version;

		public bool HasResult => _result != 0;

		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _core.GetStatus(token);
		}

		public Releaser GetResult(short token)
		{
			var result = _core.GetResult(token);
			// Reset the necessary fields before returning to the pool.
			_cancellationRegistration.Dispose();
			_core.Reset();
			_result = 0;
			_rwLock.Return(this);
			return result;
		}

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_core.OnCompleted(continuation, state, token, flags);
		}

		public void SetCancellation(CancellationToken cancellationToken)
		{
			_cancellationRegistration = cancellationToken.Register(static state => // This closure must be static to reduce allocation.
			{
				var self = (Waiter)state!;
				if (Interlocked.Exchange(ref self._result, 1) == 0)
				{
					self._rwLock.Release(ReleaseMode.Cancelled); // Release before propagation.
					self._core.SetException(new OperationCanceledException(self._cancellationRegistration.Token));
				}
			}, this);
		}

		public void SetResult(in Releaser releaser)
		{
			if (Interlocked.Exchange(ref _result, 1) == 0)
			{
				_core.SetResult(releaser);
			}
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