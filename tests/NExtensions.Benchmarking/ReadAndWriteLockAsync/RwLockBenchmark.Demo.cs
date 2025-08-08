using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

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
	[Params("1/10", "5/10", "10/10", "10/5", "10/1")]
	public string RW = "1/5";

	[Params(150_000)]
	public override int Hits { get; set; } = 10_000;

	[Params("yield")]
	public override string Wait { get; set; } = "yield";

	[Params(ContinuationMode.AsyncReadWrite)]
	public override ContinuationMode Continuation { get; set; } = ContinuationMode.AsyncReadWrite;

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

		var locker = GetAsyncReaderWriterLockEx();
		var enqueued = 0;
		var (readers, writers) = GetWorkersCount();
		var writeOptions = new ParallelOptions { MaxDegreeOfParallelism = writers };
		var enqueue = Parallel.ForAsync(0, writers, writeOptions, async (_, ct) =>
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
		var readOptions = new ParallelOptions { MaxDegreeOfParallelism = readers };
		var reading = Parallel.ForAsync(0, readers, readOptions, async (_, ct) =>
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
		var locker = GetAsyncReaderWriterLock();
		var enqueued = 0;
		var (readers, writers) = GetWorkersCount();
		var writeOptions = new ParallelOptions { MaxDegreeOfParallelism = writers };
		var enqueue = Parallel.ForAsync(0, writers, writeOptions, async (_, ct) =>
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
		var readOptions = new ParallelOptions { MaxDegreeOfParallelism = readers };
		var reading = Parallel.ForAsync(0, readers, readOptions, async (_, ct) =>
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