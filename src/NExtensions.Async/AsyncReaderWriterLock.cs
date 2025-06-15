using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

public sealed class AsyncReaderWriterLock
{
	private readonly Queue<Waiter> _readerQueue = new();
	private readonly Queue<Waiter> _writerQueue = new();
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

			var waiter = new Waiter(this, isWriter: false);
			_readerQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
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

			var waiter = new Waiter(this, isWriter: true);
			_writerQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	private void Release(bool isWriter)
	{
		Waiter? toWake = null;
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
				return;
			
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
		
		toWake?.SetResult(new Releaser(this, isWriter: true));

		if (toWakeReaders == null) return;
		foreach (var r in toWakeReaders)
			r.SetResult(new Releaser(this, isWriter: false));
	}

	public readonly struct Releaser : IDisposable
	{
		private readonly AsyncReaderWriterLock _lock;
		private readonly bool _isWriter;

		public Releaser(AsyncReaderWriterLock rwLock, bool isWriter)
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
		private ManualResetValueTaskSourceCore<Releaser> _core = new() { RunContinuationsAsynchronously = true };
		private readonly AsyncReaderWriterLock _lock;
		private readonly bool _isWriter;

		public Waiter(AsyncReaderWriterLock rwLock, bool isWriter)
		{
			_lock = rwLock;
			_isWriter = isWriter;
		}

		public short Version => _core.Version;

		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _core.GetStatus(token);
		}

		public Releaser GetResult(short token)
		{
			var result = _core.GetResult(token);
			_core.Reset(); // Reset after GetResult to allow re-awaiting (if reused in future)
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