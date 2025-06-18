using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NExtensions.Async;

// ReSharper disable InconsistentNaming

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
namespace NExtensions.Benchmarking.Benchmarks;

//[SimpleJob(warmupCount: 2, iterationCount: 8)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LockingBenchmarkLimited : LockingBenchmark
{
	[Params("1/10", "5/5", "5/10", "10/10", "10/5", "10/1")]
	public string RW = "1/5";

	[Params(50_000)]
	public override int Count { get; set; } = 50_000;

	[Params("yield")]
	public override string Wait { get; set; } = "yield";

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (int Readers, int Writers) GetWorkersCount()
	{
		var split = RW.Split('/');
		return (int.Parse(split[0]), int.Parse(split[1]));
	}

	// [Benchmark]
	// public async Task ConcurrentBag()
	// {
	// 	var inputs = new ConcurrentBag<Payload>();
	// 	var (readers, writers) = GetWorkersCount();
	// 	var writeOptions = new ParallelOptions { MaxDegreeOfParallelism = writers };
	// 	var enqueued = 0;
	// 	var enqueue = Parallel.ForAsync(0, writers, writeOptions, async (_, _) =>
	// 	{
	// 		while (true)
	// 		{
	// 			await WaitMeAsync();
	// 			var item = Interlocked.Increment(ref enqueued);
	// 			if (item > Count)
	// 				break;
	// 			inputs.Add(Payload.Default);
	// 		}
	// 	});
	//
	// 	var read = 0;
	// 	var readOptions = new ParallelOptions { MaxDegreeOfParallelism = readers };
	// 	var reading = Parallel.ForAsync(0, readers, readOptions, async (_, _) =>
	// 	{
	// 		while (true)
	// 		{
	// 			await WaitMeAsync();
	// 			if (!TryTakeLast(inputs, out var payload)) continue;
	// 			var currentRead = Interlocked.Increment(ref read);
	// 			if (currentRead > Count) break;
	// 			Bag.Add(payload);
	// 		}
	// 	});
	//
	// 	await Task.WhenAll(enqueue, reading);
	// 	ThrowIfUnMatched();
	// }
	//
	// [Benchmark(Baseline = true)]
	// public async Task ListWithSemaphore()
	// {
	// 	var inputs = new List<Payload>();
	// 	var semaphore = new SemaphoreSlim(1,1);
	// 	var enqueued = 0;
	// 	var (readers, writers) = GetWorkersCount();
	// 	var writeOptions = new ParallelOptions { MaxDegreeOfParallelism = writers };
	// 	var enqueue = Parallel.ForAsync(0, writers, writeOptions, async (_, ct) =>
	// 	{
	// 		while (true)
	// 		{
	// 			var item = Interlocked.Increment(ref enqueued);
	// 			if (item > Count)
	// 				break;
	//
	// 			await WaitMeAsync();
	// 			await semaphore.WaitAsync(ct);
	// 			try
	// 			{
	// 				inputs.Add(Payload.Default);
	// 			}
	// 			finally
	// 			{
	// 				semaphore.Release();
	// 			}
	// 		}
	// 	});
	//
	// 	var read = 0;
	// 	var readOptions = new ParallelOptions { MaxDegreeOfParallelism = readers };
	// 	var reading = Parallel.ForAsync(0, readers, readOptions, async (_, ct) =>
	// 	{
	// 		while (read < Count)
	// 		{
	// 			await WaitMeAsync();
	// 			await semaphore.WaitAsync(ct);
	// 			try
	// 			{
	// 				if (TryTakeLast(inputs, out var payload))
	// 				{
	// 					var current = Interlocked.Increment(ref read);
	// 					if (current > Count) break;
	// 					Bag.Add(payload);
	// 				}
	// 			}
	// 			finally
	// 			{
	// 				semaphore.Release();
	// 			}
	// 		}
	// 	});
	//
	// 	await Task.WhenAll(enqueue, reading);
	// 	ThrowIfUnMatched();
	// }

	[Benchmark]
	public async Task ListWithAsyncReaderWriterLock()
	{
		var inputs = new List<Payload>();
		var locker = new AsyncReaderWriterLock();
		var canceller = new CancellationTokenSource();
		var enqueued = 0;
		var (readers, writers) = GetWorkersCount();
		var writeOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = writers,
			CancellationToken = canceller.Token
		};
		var enqueue = Parallel.ForAsync(0, writers, writeOptions, async (_, ct) =>
		{
			while (true)
			{
				var item = Interlocked.Increment(ref enqueued);
				if (item > Count)
					break;
	
				
				await WaitMeAsync();
				using (await locker.WriterLockAsync(ct))
				{
					inputs.Add(Payload.Default);
				}
			}
		});
	
		var read = 0;
		var readOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = readers,
			CancellationToken = canceller.Token
		};
		var reading = Parallel.ForAsync(0, readers, readOptions, async (_, ct) =>
		{
			while (read < Count)
			{
				await WaitMeAsync();
				using (await locker.ReaderLockAsync(ct))
				{
					if (!TryTakeLast(inputs, out var payload)) continue;
					var current = Interlocked.Increment(ref read);
					if (current > Count) break;
					Bag.Add(payload);
				}
			}
		});
	
		await Task.WhenAll(enqueue, reading);
		ThrowIfUnMatched();
		canceller.Dispose();
	}
	
	[Benchmark]
	public async Task ListWithAsyncReaderWriterLockSlim()
	{
		var inputs = new List<Payload>();
		var locker = new AsyncReaderWriterLockSlim();
		var enqueued = 0;
		var (readers, writers) = GetWorkersCount();
		var writeOptions = new ParallelOptions { MaxDegreeOfParallelism = writers };
		var enqueue = Parallel.ForAsync(0, writers, writeOptions, async (_, ct) =>
		{
			while (true)
			{
				var item = Interlocked.Increment(ref enqueued);
				if (item > Count)
					break;
	
				await WaitMeAsync();
				using (await locker.WriterLockAsync(ct))
				{
					inputs.Add(Payload.Default);
				}
			}
		});
	
		var read = 0;
		var readOptions = new ParallelOptions { MaxDegreeOfParallelism = readers };
		var reading = Parallel.ForAsync(0, readers, readOptions, async (_, ct) =>
		{
			while (read < Count)
			{
				await WaitMeAsync();
				using (await locker.ReaderLockAsync(ct))
				{
					if (!TryTakeLast(inputs, out var payload)) continue;
					var current = Interlocked.Increment(ref read);
					if (current > Count) break;
					Bag.Add(payload);
				}
			}
		});
	
		await Task.WhenAll(enqueue, reading);
		ThrowIfUnMatched();
	}
}