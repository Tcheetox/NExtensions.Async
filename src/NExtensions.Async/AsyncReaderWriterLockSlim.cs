using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

public sealed class AsyncReaderWriterLockSlim
{
	private readonly Queue<Waiter> _readerQueue = new();
	private readonly Queue<Waiter> _writerQueue = new();
	private readonly Stack<Waiter> _waiterPool = new();
	private readonly object _sync = new();
	private int _readerCount;
	private bool _writerActive;
	
	public ValueTask<Releaser> ReaderLockAsync()
	{
		lock (_sync)
		{
			if (!_writerActive && _writerQueue.Count == 0)
			{
				_readerCount++;
				return new ValueTask<Releaser>(new Releaser(this, false));
			}

			var waiter = Rent();
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
	
	public ValueTask<Releaser> WriterLockAsync()
	{
		lock (_sync)
		{
			if (!_writerActive && _readerCount == 0)
			{
				_writerActive = true;
				return new ValueTask<Releaser>(new Releaser(this, true));
			}

			var waiter = Rent();
			_writerQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	private void Release(bool isWriter)
	{
		Waiter? toWake;
		List<Waiter>? toWakeReaders = null;

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
			
			if (_writerQueue.TryDequeue(out toWake))
			{
				_writerActive = true;
			}
			else if (_readerQueue.Count > 0)
			{
				toWakeReaders = new List<Waiter>(_readerQueue.Count);
				while (_readerQueue.TryDequeue(out var r))
				{
					_readerCount++;
					toWakeReaders.Add(r);
				}
			}
		}

		Debug.Assert(toWake == null || toWakeReaders == null, "Cannot wake both writer and readers simultaneously.");
		
		// Do not call SetResult in the lock to avoid Lock contentions due to reentrancy (even though continuation is run async).
		toWake?.SetResult(new Releaser(this, isWriter: true));
		if (toWakeReaders == null) return;
		foreach (var r in toWakeReaders)
			r.SetResult(new Releaser(this, isWriter: false));
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
		private ManualResetValueTaskSourceCore<Releaser> _core = new()
		{
			RunContinuationsAsynchronously = true // Ensures no reentrancy and prevents the consumers to hijack the thread.
		};
		public short Version => _core.Version;

		private readonly AsyncReaderWriterLockSlim _rwLock;
		public Waiter(AsyncReaderWriterLockSlim rwLock)
		{
			_rwLock = rwLock;
		}
		
		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _core.GetStatus(token);
		}

		public Releaser GetResult(short token)
		{
			var result = _core.GetResult(token);
			_core.Reset(); // Reset after GetResult to allow re-awaiting (if reused in future).
			_rwLock.Return(this);
			return result;
		}

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_core.OnCompleted(continuation, state, token, flags);
		}

		public void SetResult(Releaser releaser)
		{
			_core.SetResult(releaser);
		}
	}
}