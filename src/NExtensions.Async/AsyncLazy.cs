using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NExtensions.Async;

// TODO: rename methods in rwasynclokc
// TODO: comments on the below
// TODO: readme
// TODO: pipeline
public enum LazyAsyncThreadSafetyMode
{
	None, // No guarantee, no retry
	NoneWithRetry, // No guarantee, only success are stored for good
	PublicationOnly, // Guaranteed same value for every consumer, no retry
	PublicationOnlyWithRetry,
	ExecutionAndPublication,
	ExecutionAndPublicationWithRetry
}

public class AsyncLazy<T>
{
	private readonly LazyAsyncThreadSafetyMode _mode;
	private readonly AsyncLock _sync = new();

	private Func<CancellationToken, Task<T>> _factory;
	private Task<T>? _value;

	public AsyncLazy(Func<Task<T>> valueFactory,
		LazyAsyncThreadSafetyMode mode = LazyAsyncThreadSafetyMode.ExecutionAndPublication)
		: this(_ => valueFactory(), mode)
	{
	}

	public AsyncLazy(Func<CancellationToken, Task<T>> valueFactory,
		LazyAsyncThreadSafetyMode mode = LazyAsyncThreadSafetyMode.ExecutionAndPublication)
	{
		ArgumentNullException.ThrowIfNull(valueFactory);
		_factory = valueFactory;
		_mode = mode;
	}

	// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
	internal bool HasFactory => _factory is not null;

	public bool IsRetryable => _mode is LazyAsyncThreadSafetyMode.NoneWithRetry
		or LazyAsyncThreadSafetyMode.ExecutionAndPublicationWithRetry
		or LazyAsyncThreadSafetyMode.PublicationOnlyWithRetry;

	public TaskAwaiter<T> GetAwaiter()
	{
		return GetValueAsync(CancellationToken.None).GetAwaiter();
	}

	public Task<T> GetValueAsync(CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return Task.FromCanceled<T>(cancellationToken);

		var current = _value;
		if (current is not null)
			return current; // Fast path.

		return _mode switch
		{
			LazyAsyncThreadSafetyMode.None => GetNoneAsync(cancellationToken),
			LazyAsyncThreadSafetyMode.NoneWithRetry => GetNoneWithRetryAsync(cancellationToken),
			LazyAsyncThreadSafetyMode.PublicationOnly => GetPublicationOnlyAsync(cancellationToken),
			LazyAsyncThreadSafetyMode.PublicationOnlyWithRetry => GetPublicationOnlyWithRetryAsync(cancellationToken),
			LazyAsyncThreadSafetyMode.ExecutionAndPublication => GetExecutionAndPublicationAsync(cancellationToken),
			LazyAsyncThreadSafetyMode.ExecutionAndPublicationWithRetry => GetExecutionAndPublicationWithRetryAsync(cancellationToken),
			_ => throw new NotSupportedException($"Mode {_mode} is not supported.")
		};
	}

	private async Task<T> GetNoneAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			_value = _factory(cancellationToken);
			return await _value.ConfigureAwait(false);
		}
		catch (Exception e)
		{
			_value ??= Task.FromException<T>(e);
			throw;
		}
		finally
		{
			_factory = null!;
		}
	}

	private async Task<T> GetNoneWithRetryAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var local = _factory(cancellationToken);
			var result = await local.ConfigureAwait(false);
			_value = local;
			_factory = null!;
			return result;
		}
		catch
		{
			_value = null;
			throw;
		}
	}

	private async Task<T> GetPublicationOnlyAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var local = _factory(cancellationToken);
			var published = Interlocked.CompareExchange(ref _value, local, null);
			return await (published ?? local).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			var error = Task.FromException<T>(ex);
			var published = Interlocked.CompareExchange(ref _value, error, null) ?? error;
			Debug.Assert(published.IsCompleted);
			return published.GetAwaiter().GetResult();
		}
		finally
		{
			_factory = null!;
		}
	}

	private async Task<T> GetPublicationOnlyWithRetryAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var local = _factory(cancellationToken);
			_ = await local.ConfigureAwait(false);
			var published = Interlocked.CompareExchange(ref _value, local, null) ?? local;
			Debug.Assert(published.IsCompletedSuccessfully);
			return published.Result; // Safe since we must be in a completed state. 
		}
		catch (Exception ex)
		{
			// Do not store exception when retryable, but check still check if another thread might have succeeded in the meantime.
			var published = Interlocked.CompareExchange(ref _value, null, null) ?? Task.FromException<T>(ex);
			Debug.Assert(published.IsCompleted);
			return published.GetAwaiter().GetResult();
		}
		finally
		{
			var local = _value;
			if (local is not null && local.IsCompletedSuccessfully)
				_factory = null!;
		}
	}

	private async Task<T> GetExecutionAndPublicationAsync(CancellationToken cancellationToken = default)
	{
		// No retry policy, do not await within the semaphore to ensure quick release if not awaited directly by the caller.
		Task<T> local;
		using (await _sync.EnterScopeAsync(cancellationToken))
		{
			try
			{
				local = _value ??= _factory(cancellationToken);
			}
			catch (Exception ex)
			{
				_value ??= Task.FromException<T>(ex);
				throw;
			}
			finally
			{
				_factory = null!;
				Debug.Assert(_value is not null);
			}
		}

		return await local.ConfigureAwait(false);
	}

	private async Task<T> GetExecutionAndPublicationWithRetryAsync(CancellationToken cancellationToken = default)
	{
		using (await _sync.EnterScopeAsync(cancellationToken))
		{
			var local = _value ?? _factory(cancellationToken);
			var result = await local.ConfigureAwait(false);
			_value = local;
			_factory = null!;
			return result;
		}
	}

	#region Snapshot

	public bool IsValueCreated => _value != null;
	public bool IsCompleted => _value?.IsCompleted ?? false;
	public bool IsFaulted => _value?.IsFaulted ?? false;
	public bool IsCanceled => _value?.IsCanceled ?? false;
	public bool IsCompletedSuccessfully => _value?.IsCompletedSuccessfully ?? false;

	#endregion
}