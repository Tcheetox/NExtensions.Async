using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

[DebuggerDisplay("Readers={_readerCount}/{_readerQueue.Count}, Writers={_writerActive}/{_writerQueue.Count}, Pool={_waiterPool.Count}")]
public sealed class AsyncReaderWriterLock
{
	public enum ReleaseMode
	{
		Faulted,
		Writer,
		Reader
	}

	private readonly Queue<Waiter> _readerQueue = new();
	private readonly object _sync = new();
	private readonly Stack<Waiter> _waiterPool = new();
	private readonly Queue<Waiter> _writerQueue = new();
	private int _readerCount;
	private bool _writerActive;

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
			if (cancellationToken.CanBeCanceled)
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
				case ReleaseMode.Faulted:
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
				// TODO: KRE, check if for performance that wouldn't be worth checking if there's a single reader queued?
				readersToWake = new List<Waiter>(_readerQueue.Count);
				while (_readerQueue.TryDequeue(out var waiter) && !waiter.HasResult) // Dismiss waiters which have canceled.
					readersToWake.Add(waiter);
				_readerCount += readersToWake.Count; // Readers about to pick up (outside the lock).
			}
		}

		Debug.Assert(writerToWake == null || readersToWake == null, "Cannot wake both writer and readers simultaneously.");

		// Do not call SetResult in the lock to avoid Lock contentions due to reentrancy (even though continuation is run async).
		writerToWake?.SetResult(new Releaser(this, ReleaseMode.Writer));
		if (readersToWake == null) return;
		foreach (var reader in readersToWake)
			reader.SetResult(new Releaser(this, ReleaseMode.Reader));
	}

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
			_cancellationRegistration = cancellationToken.Register(static state =>
			{
				var self = (Waiter)state!;
				if (Interlocked.Exchange(ref self._result, 1) == 0)
				{
					self._rwLock.Release(ReleaseMode.Faulted); // Release before propagation.
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