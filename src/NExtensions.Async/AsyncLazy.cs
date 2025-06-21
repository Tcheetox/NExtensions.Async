using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NExtensions.Async;

public enum LazyRetryPolicy
{
	None,
	Retry,
	StrictRetry
}

public class AsyncLazy<T>
{
	private readonly Func<CancellationToken, Task<T>> _factory;
	private readonly LazyThreadSafetyMode _mode;
	private readonly LazyRetryPolicy _policy;

	private readonly Lazy<SemaphoreSlim> _sync = new(() => new SemaphoreSlim(1, 1));

	private Task<T>? _value;

	public AsyncLazy(Func<Task<T>> valueFactory,
		LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication,
		LazyRetryPolicy policy = LazyRetryPolicy.None)
		: this(_ => valueFactory(), mode, policy)
	{
	}

	public AsyncLazy(Func<CancellationToken, Task<T>> valueFactory,
		LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication,
		LazyRetryPolicy policy = LazyRetryPolicy.None)
	{
		ArgumentNullException.ThrowIfNull(valueFactory);
		_factory = valueFactory;
		_mode = mode;
		_policy = policy;
	}

	public bool IsValueCreated => _value != null;
	public bool IsRetryable => _policy is LazyRetryPolicy.Retry or LazyRetryPolicy.StrictRetry;
	public bool IsCompleted => _value?.IsCompleted ?? false;
	public bool IsFaulted => _value?.IsFaulted ?? false;
	public bool IsCanceled => _value?.IsCanceled ?? false;

	public TaskAwaiter<T> GetAwaiter()
	{
		return GetValueAsync(CancellationToken.None).GetAwaiter();
	}

	public Task<T> GetValueAsync(CancellationToken cancellationToken = default)
	{
		var current = _value;
		if (current is not null)
			return current; // Fast path.

		return _mode switch
		{
			// No guarantees.
			LazyThreadSafetyMode.None => GetNoneValueAsync(cancellationToken),
			// Same exposure for everyone.
			LazyThreadSafetyMode.PublicationOnly => GetPublicationOnlyValueAsync(cancellationToken),
			// Full protection.
			LazyThreadSafetyMode.ExecutionAndPublication => GetExecutionAndPublicationValueAsync(cancellationToken),
			_ => throw new NotSupportedException($"Mode {_mode} is not supported.")
		};
	}

	private async Task<T> GetNoneValueAsync(CancellationToken cancellationToken = default)
	{
		switch (_policy)
		{
			case LazyRetryPolicy.Retry:
				try
				{
					_value = _factory(cancellationToken); // Everyone can await but there's only one task that will actually reset the value.
					return await _value;
				}
				catch
				{
					_value = null;
					throw;
				}

			case LazyRetryPolicy.StrictRetry:
				try
				{
					var local = _factory(cancellationToken);
					var result = await local;
					_value = local;
					return result;
				}
				catch
				{
					_value = null;
					throw;
				}

			// No try-catch block overhead.
			case LazyRetryPolicy.None:
			default:
				_value = _factory(cancellationToken);
				return await _value;
		}
	}

	private async Task<T> GetPublicationOnlyValueAsync(CancellationToken cancellationToken = default)
	{
		switch (_policy)
		{
			case LazyRetryPolicy.None:
			{
				var local = _factory(cancellationToken);
				var published = Interlocked.CompareExchange(ref _value, local, null);
				return await (published ?? local);
			}

			case LazyRetryPolicy.Retry:
				try
				{
					var local = _factory(cancellationToken);
					var published = Interlocked.CompareExchange(ref _value, local, null);
					return await (published ?? local);
				}
				catch
				{
					_ = Interlocked.Exchange(ref _value, null);
					throw;
				}

			case LazyRetryPolicy.StrictRetry:
			default:
#if DEBUG
				try
				{
#endif
					var local = _factory(cancellationToken);
					_ = await local; // Ensures local gets completed, only swap on success.
					var published = Interlocked.CompareExchange(ref _value, local, null) ?? local;
					Debug.Assert(published.IsCompletedSuccessfully,
						$"Only success must be published with this combination of {LazyThreadSafetyMode.PublicationOnly} and {LazyRetryPolicy.Retry}.");
					return await published;
#if DEBUG
				}
				catch
				{
					Debug.Assert(_value is null);
					throw;
				}
#endif
		}
	}

	private async Task<T> GetExecutionAndPublicationValueAsync(CancellationToken cancellationToken = default)
	{
		if (_policy is not LazyRetryPolicy.None)
		{
			await _sync.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (_value is not null)
					return await _value.ConfigureAwait(false);

				if (_policy is LazyRetryPolicy.Retry)
				{
					_value = _factory(cancellationToken);
					return await _value;
				}

				var local = _factory(cancellationToken);
				var result = await local;
				_value = local;
				return result;
			}
			catch
			{
				_value = null;
				throw;
			}
			finally
			{
				_sync.Value.Release();
			}
		}

		await _sync.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_value is not null)
				return await _value.ConfigureAwait(false);
			_value = _factory(cancellationToken);
			return await _value;
		}
		finally
		{
			_sync.Value.Release();
		}
	}
}