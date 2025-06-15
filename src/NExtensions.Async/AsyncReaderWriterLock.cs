using System.Collections.Concurrent;
using System.Threading.Tasks.Sources;

namespace NExtensions.Async;

public sealed class AsyncReaderWriterLock
{
	private readonly ConcurrentQueue<Waiter> _readerQueue = new();
	private readonly object _sync = new();
	private readonly WaiterPool _waiterPool = new();
	private readonly ConcurrentQueue<Waiter> _writerQueue = new();
	private int _readerCount;
	private bool _writerActive;

	public ValueTask<Releaser> ReaderLockAsync()
	{
		lock (_sync)
		{
			if (!_writerActive && _writerQueue.IsEmpty)
			{
				_readerCount++;
				return new ValueTask<Releaser>(new Releaser(this, false));
			}

			var waiter = _waiterPool.Rent();
			waiter.Initialize(this, false);
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

			var waiter = _waiterPool.Rent();
			waiter.Initialize(this, true);
			_writerQueue.Enqueue(waiter);
			return new ValueTask<Releaser>(waiter, waiter.Version);
		}
	}

	private void Release(bool isWriter)
	{
		Waiter? toWake = null;
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

			if (!_writerActive && _readerCount == 0 && _writerQueue.TryDequeue(out toWake))
			{
				_writerActive = true;
			}
			else if (!_writerActive && _writerQueue.IsEmpty)
			{
				while (_readerQueue.TryDequeue(out var reader))
				{
					_readerCount++;
					reader.SetResult(new Releaser(this, false));
				}
			}
		}

		toWake?.SetResult(new Releaser(this, true));
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

	private class Waiter : IValueTaskSource<Releaser>
	{
		private ManualResetValueTaskSourceCore<Releaser> _core; // struct
		private bool _isWriter;
		private AsyncReaderWriterLock _lock = null!;

		public short Version => _core.Version;

		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _core.GetStatus(token);
		}

		public Releaser GetResult(short token)
		{
			return _core.GetResult(token);
		}

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_core.OnCompleted(continuation, state, token, flags);
		}

		public void Initialize(AsyncReaderWriterLock rwLock, bool isWriter)
		{
			_lock = rwLock;
			_isWriter = isWriter;
			_core.Reset();
		}

		public void SetResult(Releaser releaser)
		{
			_core.SetResult(releaser);
			_lock._waiterPool.Return(this);
		}
	}

	private class WaiterPool
	{
		private readonly ConcurrentQueue<Waiter> _pool = new();

		public Waiter Rent()
		{
			return _pool.TryDequeue(out var waiter) ? waiter : new Waiter();
		}

		public void Return(Waiter waiter)
		{
			_pool.Enqueue(waiter);
		}
	}
}