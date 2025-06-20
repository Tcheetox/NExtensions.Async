using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

public sealed class AsyncReaderWriterLockSlim
{
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
				return new ValueTask<Releaser>(new Releaser(this, false));
			}

			var waiter = Rent();
			if (cancellationToken.CanBeCanceled)
				waiter.SetCancellation(cancellationToken);
			_readerQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

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

	public ValueTask<Releaser> WriterLockAsync(CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return ValueTask.FromCanceled<Releaser>(cancellationToken);

		lock (_sync)
		{
			if (!_writerActive && _readerCount == 0)
			{
				_writerActive = true;
				return new ValueTask<Releaser>(new Releaser(this, true));
			}

			var waiter = Rent();
			if (cancellationToken.CanBeCanceled)
				waiter.SetCancellation(cancellationToken);
			_writerQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	private void Release(bool isWriter)
	{
		Waiter? writerToWake;
		List<Waiter>? readersToWake = null;

		lock (_sync)
		{
			if (isWriter)
			{
				_writerActive = false;
			}
			else
			{
				_readerCount--;
			}

			if (_writerActive || _readerCount > 0)
				return; // Early exit.

			// Attempt to get a writer.
			while (_writerQueue.TryDequeue(out writerToWake))
			{
				if (writerToWake.HasResult)
					continue;
				_writerActive = true;
				break;
			}

			// Check for readers if no writers.
			if (writerToWake is null && _readerQueue.Count > 0)
			{
				readersToWake = new List<Waiter>(_readerQueue.Count);
				while (_readerQueue.TryDequeue(out var waiter) && !waiter.HasResult) // Dismiss waiters which have canceled.
					readersToWake.Add(waiter);
				_readerCount += readersToWake.Count; // Readers about to pick up (outside the lock).
			}
		}

		Debug.Assert(writerToWake == null || readersToWake == null, "Cannot wake both writer and readers simultaneously.");

		// Do not call SetResult in the lock to avoid Lock contentions due to reentrancy (even though continuation is run async).
		writerToWake?.SetResult(new Releaser(this, true));
		if (readersToWake == null) return;
		foreach (var r in readersToWake)
			r.SetResult(new Releaser(this, false));
	}

	public readonly struct Releaser : IDisposable
	{
		private readonly AsyncReaderWriterLockSlim _lock;
		private readonly bool _isWriter;

		public Releaser(AsyncReaderWriterLockSlim rwLock, bool isWriter)
		{
			_lock = rwLock;
			_isWriter = isWriter;
		}

		public void Dispose()
		{
			_lock.Release(_isWriter);
		}
	}

	private sealed class Waiter : IValueTaskSource<Releaser>
	{
		private readonly AsyncReaderWriterLockSlim _rwLock;
		private readonly object _sync = new();
		private CancellationTokenRegistration _cancellationRegistration;

		private ManualResetValueTaskSourceCore<Releaser> _core = new()
		{
			// Ensures no reentrancy and prevents the consumers to hijack the thread.
			RunContinuationsAsynchronously = true
		};

		private int _result;

		public Waiter(AsyncReaderWriterLockSlim rwLock)
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
			_cancellationRegistration.Dispose();
			_core.Reset(); // Reset after GetResult to allow re-awaiting (if reused in future).
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
				lock (self._sync)
				{
					if (self._result == 0) self._result = 1;
					else return;
				}

				self._core.SetException(new OperationCanceledException(self._cancellationRegistration.Token));
			}, this);
		}

		public void SetResult(Releaser releaser)
		{
			lock (_sync)
			{
				if (_result == 0) _result = 1;
				else return;
			}

			_core.SetResult(releaser);
		}
	}
}