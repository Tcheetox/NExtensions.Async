using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Nito.AsyncEx;

// ReSharper disable InconsistentNaming
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
namespace NExtensions.Benchmarking.ReadAndWriteLockAsync;

//[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class RwLockBenchmarkDemo : RwLockBenchmark
{
	private AsyncReaderWriterLock _asyncExLock = null!;
	private Async.AsyncReaderWriterLock _asyncLock = null!;
	private ParallelOptions _readOptions = null!;
	private ParallelOptions _writeOptions = null!;

	[Params("1/10", "5/10", "10/10", "10/5", "10/1")]
	public string RW = "1/5";

	[Params(150_000)]
	public override int Hits { get; set; } = 10_000;

	[Params("yield")]
	public override string Wait { get; set; } = "yield";

	//[Params(ContinuationMode.AsyncReadWrite)]
	public override ContinuationMode Continuation { get; set; } = ContinuationMode.AsyncReadWrite;

	[GlobalSetup]
	public void Setup()
	{
		_asyncExLock = GetAsyncReaderWriterLockEx();
		_asyncLock = GetAsyncReaderWriterLock();
		var (readers, writers) = GetWorkersCount();
		_readOptions = new ParallelOptions { MaxDegreeOfParallelism = readers };
		_writeOptions = new ParallelOptions { MaxDegreeOfParallelism = writers };
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (int Readers, int Writers) GetWorkersCount()
	{
		var split = RW.Split('/');
		return (int.Parse(split[0]), int.Parse(split[1]));
	}

	[Benchmark]
	public async Task AsyncExReaderWriterLock()
	{
		if (Continuation != ContinuationMode.AsyncReadWrite)
			throw new InvalidBenchmarkException("Noop.");

		var locker = _asyncExLock;
		var enqueued = 0;
		var enqueue = Parallel.ForAsync(0, _writeOptions.MaxDegreeOfParallelism, _writeOptions, async (_, ct) =>
		{
			while (true)
			{
				var item = Interlocked.Increment(ref enqueued);
				if (item > Hits) break;

				using (await locker.WriterLockAsync(ct))
				{
					await Utility.WaitMeAsync(Wait);
				}
			}
		});

		var read = 0;
		var reading = Parallel.ForAsync(0, _readOptions.MaxDegreeOfParallelism, _readOptions, async (_, ct) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref read);
				if (current > Hits) break;

				using (await locker.ReaderLockAsync(ct))
				{
					await Utility.WaitMeAsync(Wait);
				}
			}
		});

		await Task.WhenAll(enqueue, reading);
	}

	[Benchmark]
	public async Task AsyncReaderWriterLock()
	{
		var locker = _asyncLock;
		var enqueued = 0;
		var enqueue = Parallel.ForAsync(0, _writeOptions.MaxDegreeOfParallelism, _writeOptions, async (_, ct) =>
		{
			while (true)
			{
				var item = Interlocked.Increment(ref enqueued);
				if (item > Hits) break;

				using (await locker.EnterWriterScopeAsync(ct))
				{
					await Utility.WaitMeAsync(Wait);
				}
			}
		});

		var read = 0;
		var reading = Parallel.ForAsync(0, _readOptions.MaxDegreeOfParallelism, _readOptions, async (_, ct) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref read);
				if (current > Hits) break;

				using (await locker.EnterReaderScopeAsync(ct))
				{
					await Utility.WaitMeAsync(Wait);
				}
			}
		});

		await Task.WhenAll(enqueue, reading);
	}
}