using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NExtensions.Async;
using AsyncReaderWriterLockEx = Nito.AsyncEx.AsyncReaderWriterLock;

// ReSharper disable MemberCanBeProtected.Global
namespace NExtensions.Benchmarking.ReadAndWriteLockAsync;

public abstract class RwLockBenchmark
{
	public enum ContinuationMode
	{
		SyncRead,
		SyncWrite,
		SyncReadWrite,
		AsyncReadWrite
	}

	[Params(100, 10_000)]
	public virtual int Hits { get; set; } = 10_000;

	[Params("yield", "delay", "sync")]
	public virtual string Wait { get; set; } = "delay";

	[Params(
		ContinuationMode.SyncRead,
		ContinuationMode.SyncReadWrite,
		ContinuationMode.SyncWrite,
		ContinuationMode.AsyncReadWrite
	)]
	public virtual ContinuationMode Continuation { get; set; } = ContinuationMode.AsyncReadWrite;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected AsyncReaderWriterLockEx GetAsyncReaderWriterLockEx()
	{
		if (Continuation != ContinuationMode.AsyncReadWrite)
			throw new InvalidBenchmarkException("Noop.");
		return new AsyncReaderWriterLockEx();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected AsyncReaderWriterLock GetAsyncReaderWriterLock()
	{
		switch (Continuation)
		{
			case ContinuationMode.SyncRead:
				return new AsyncReaderWriterLock(true, false);
			case ContinuationMode.SyncReadWrite:
				return new AsyncReaderWriterLock(true, true);
			case ContinuationMode.SyncWrite:
				return new AsyncReaderWriterLock(false, true);
			case ContinuationMode.AsyncReadWrite:
			default:
				return new AsyncReaderWriterLock();
		}
	}

	protected static bool TryTakeLast<T>(IEnumerable<T> enumerable, [NotNullWhen(true)] out T? item) where T : class
	{
		item = null;
		foreach (var entry in enumerable)
			item = entry;
		return item is not null;
	}

	protected static bool TryTakeOne<T>(IEnumerable<T> enumerable, [NotNullWhen(true)] out T? item) where T : class
	{
		item = null;
		foreach (var entry in enumerable)
		{
			item = entry;
			// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			if (item is not null)
				return true;
		}

		return item is not null;
	}

	protected sealed class Payload
	{
		public static readonly Payload Default = new(-1);

		// ReSharper disable once NotAccessedField.Local
		public readonly int Value;

		// ReSharper disable once MemberCanBePrivate.Global
		public Payload(int value)
		{
			Value = value;
		}
	}
}