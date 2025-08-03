using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NExtensions.Async;

/// <summary>
/// Specifies the thread-safety and retry behavior modes for <see cref="AsyncLazy{T}"/> initialization.
/// Heavily inspired from <see cref="LazyThreadSafetyMode"/>.
/// </summary>
public enum LazyAsyncSafetyMode
{
	/// <summary>
	/// No thread-safety guarantees, and no retry on failure.
	/// Matches the definition of <see cref="LazyThreadSafetyMode.None"/>.
	/// </summary>
	None,

	/// <summary>
	/// No thread-safety guarantees, but retry on failure until success is stored.
	/// </summary>
	NoneWithRetry,

	/// <summary>
	/// Guarantees that all consumers get the same successful value if any, retry on failure until success is stored.
	/// Matches the definition of <see cref="LazyThreadSafetyMode.PublicationOnly"/>.
	/// This is referred to as Publication in the field names.
	/// </summary>
	/// <remarks>In case of failures, those are not published but returned individually.</remarks>
	PublicationOnly,

	/// <summary>
	/// Guarantees thread-safe execution and publication, no retry on failure.
	/// Matches the definition of <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>.
	/// Any exception is cached and published.
	/// </summary>
	ExecutionAndPublication,

	/// <summary>
	/// Guarantees thread-safe execution and publication, retry on failure until success is stored for good.
	/// Somehow similar to <see cref="PublicationOnly"/>, but ensures a single thread executes the factory and await its result at once.
	/// </summary>
	/// <remarks>In case of failures, those are not published but returned individually.</remarks>
	ExecutionAndPublicationWithRetry
}

/// <summary>
/// Provides support for asynchronous lazy initialization with configurable thread-safety and retry semantics.
/// The value is initialized asynchronously on demand and cached according to the specified <see cref="LazyAsyncSafetyMode"/>.
/// </summary>
/// <typeparam name="T">The type of the lazily initialized value.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class AsyncLazy<T>
{
	private readonly LazyAsyncSafetyMode _mode;
	private readonly AsyncLock? _sync;

	private object _factory;
	private Task<T>? _value;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class with a factory method that does not accept a cancellation token.
	/// </summary>
	/// <param name="valueFactory">The asynchronous delegate that produces the lazily initialized value.</param>
	/// <param name="mode">
	/// The safety mode controlling how the initialization is synchronized and retried.
	/// Defaults to <see cref="LazyAsyncSafetyMode.ExecutionAndPublication"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="valueFactory"/> is <c>null</c>.</exception>
	public AsyncLazy(Func<Task<T>> valueFactory, LazyAsyncSafetyMode mode = LazyAsyncSafetyMode.ExecutionAndPublication)
	{
		ArgumentNullException.ThrowIfNull(valueFactory);
		_factory = valueFactory;
		_mode = mode;
		if (mode is LazyAsyncSafetyMode.ExecutionAndPublication or LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry)
			_sync = new AsyncLock(true);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class with a factory method that accepts a cancellation token.
	/// </summary>
	/// <param name="valueFactory">The asynchronous delegate that produces the lazily initialized value with cancellation support.</param>
	/// <param name="mode">
	/// The safety mode controlling how the initialization is synchronized and retried.
	/// Defaults to <see cref="LazyAsyncSafetyMode.ExecutionAndPublication"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="valueFactory"/> is <c>null</c>.</exception>
	public AsyncLazy(Func<CancellationToken, Task<T>> valueFactory, LazyAsyncSafetyMode mode = LazyAsyncSafetyMode.ExecutionAndPublication)
	{
		ArgumentNullException.ThrowIfNull(valueFactory);
		_factory = valueFactory;
		_mode = mode;
		if (mode is LazyAsyncSafetyMode.ExecutionAndPublication or LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry)
			_sync = new AsyncLock(true);
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private string DebuggerDisplay => $"Mode={_mode}, HasValue={IsValueCreated}, IsRetryable={IsRetryable}";

	// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
	/// <summary>
	/// Gets a value indicating whether the current thread-safety mode supports retry on failure.
	/// </summary>
	internal bool HasFactory => _factory is not null;

	/// <summary>
	/// Gets a value indicating whether the current thread-safety mode supports retry on failure.
	/// </summary>
	public bool IsRetryable => _mode is LazyAsyncSafetyMode.NoneWithRetry
		or LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry
		or LazyAsyncSafetyMode.PublicationOnly;

	/// <summary>
	/// Gets an awaiter used to await the lazily initialized value.
	/// </summary>
	/// <returns>A task awaiter for the lazily initialized value.</returns>
	public TaskAwaiter<T> GetAwaiter()
	{
		return GetAsync(CancellationToken.None).GetAwaiter();
	}

	/// <summary>
	/// Gets the lazily initialized value asynchronously, supporting cancellation.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token to cancel the initialization.</param>
	/// <returns>A task that represents the asynchronous initialization operation. The task result is the lazily initialized value.</returns>
	/// <remarks>This method is only relevant when combined with the constructor that takes a factory supporting cancellation tokens.</remarks>
	public Task<T> GetAsync(CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return Task.FromCanceled<T>(cancellationToken);

		var current = _value;
		if (current is not null)
			return current; // Fast path.

		return _mode switch
		{
			LazyAsyncSafetyMode.None => GetNoneAsync(cancellationToken),
			LazyAsyncSafetyMode.NoneWithRetry => GetNoneWithRetryAsync(cancellationToken),
			LazyAsyncSafetyMode.PublicationOnly => GetPublicationOnlyWithRetryAsync(cancellationToken), // PublicationOnly mode is always retryable.
			LazyAsyncSafetyMode.ExecutionAndPublication => GetExecutionAndPublicationAsync(cancellationToken),
			LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry => GetExecutionAndPublicationWithRetryAsync(cancellationToken),
			_ => throw new NotSupportedException($"Mode {_mode} is not supported.")
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Task<T> CreateWithOrWithoutCancellation(CancellationToken cancellationToken = default)
	{
		if (_factory is Func<Task<T>> direct)
			return direct();
		return ((Func<CancellationToken, Task<T>>)_factory)(cancellationToken);
	}

	private async Task<T> GetNoneAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			_value = CreateWithOrWithoutCancellation(cancellationToken);
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
			var local = CreateWithOrWithoutCancellation(cancellationToken);
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

	private async Task<T> GetPublicationOnlyWithRetryAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var local = CreateWithOrWithoutCancellation(cancellationToken);
			_ = await local.ConfigureAwait(false);
			var published = Interlocked.CompareExchange(ref _value, local, null) ?? local;
			Debug.Assert(published.IsCompletedSuccessfully);
			return published.Result; // Safe since we must be in a completed state. 
		}
		catch (Exception ex)
		{
			// Do not store exception when retryable, but still check if another thread might have succeeded in the meantime.
			var published = Interlocked.CompareExchange(ref _value, null, null) ?? Task.FromException<T>(ex);
			Debug.Assert(published.IsCompleted);
			return published.GetAwaiter().GetResult();
		}
		finally
		{
			var local = Volatile.Read(ref _value);
			if (local is not null && local.IsCompletedSuccessfully)
				_factory = null!;
		}
	}

	private async Task<T> GetExecutionAndPublicationAsync(CancellationToken cancellationToken = default)
	{
		// No retry policy, do not await within the lock to ensure quick release if not awaited directly by the caller.
		Task<T> local;
		using (await _sync!
			       .EnterScopeAsync(cancellationToken)
			       .ConfigureAwait(false))
		{
			try
			{
				local = _value ??= CreateWithOrWithoutCancellation(cancellationToken);
			}
			catch (Exception ex)
			{
				_value ??= Task.FromException<T>(ex);
				throw;
			}
			finally
			{
				_factory = null!;
				Debug.Assert(_value is not null, "We either store the factory exception, or the legit task (which can still throw when awaited).");
			}
		}

		return await local.ConfigureAwait(false);
	}

	private async Task<T> GetExecutionAndPublicationWithRetryAsync(CancellationToken cancellationToken = default)
	{
		using (await _sync!
			       .EnterScopeAsync(cancellationToken)
			       .ConfigureAwait(false))
		{
			var local = _value ?? CreateWithOrWithoutCancellation(cancellationToken);
			var result = await local.ConfigureAwait(false);
			_value = local;
			_factory = null!;
			return result;
		}
	}

	#region Snapshot

	/// <summary>
	/// Gets a value indicating whether the asynchronous value has been created.
	/// </summary>
	/// <remarks>Not stable unless succeeded or when the chosen mode is not <see cref="IsRetryable"/>.</remarks>
	public bool IsValueCreated => _value != null;

	/// <summary>
	/// Gets a value indicating whether the asynchronous operation has completed.
	/// </summary>
	/// <remarks>Not stable unless succeeded or when the chosen mode is not <see cref="IsRetryable"/>.</remarks>
	public bool IsCompleted => _value?.IsCompleted ?? false;

	/// <summary>
	/// Gets a value indicating whether the asynchronous operation has faulted.
	/// </summary>
	/// <remarks>Not stable unless succeeded or when the chosen mode is not <see cref="IsRetryable"/>.</remarks>
	public bool IsFaulted => _value?.IsFaulted ?? false;

	/// <summary>
	/// Gets a value indicating whether the asynchronous operation has been canceled.
	/// </summary>
	/// <remarks>Not stable unless succeeded or when the chosen mode is not <see cref="IsRetryable"/>.</remarks>
	public bool IsCanceled => _value?.IsCanceled ?? false;

	/// <summary>
	/// Gets a value indicating whether the asynchronous operation has completed successfully.
	/// </summary>
	/// <remarks>Not stable unless succeeded or when the chosen mode is not <see cref="IsRetryable"/>.</remarks>
	public bool IsCompletedSuccessfully => _value?.IsCompletedSuccessfully ?? false;

	#endregion
}