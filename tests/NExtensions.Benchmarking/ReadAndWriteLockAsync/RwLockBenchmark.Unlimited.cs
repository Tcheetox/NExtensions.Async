using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using NExtensions.Async;

// ReSharper disable AccessToDisposedClosure

namespace NExtensions.Benchmarking.ReadAndWriteLockAsync;

[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class RwLockBenchmarkUnlimited : RwLockBenchmark
{
	[Benchmark]
	public async Task ConcurrentBag()
	{
		var inputs = new ConcurrentBag<Payload>();
		var enqueue = Enumerable.Range(0, Count).Select(async _ =>
		{
			await WaitMeAsync();
			inputs.Add(Payload.Default);
		});

		var read = 0;
		var reading = Enumerable.Range(0, Count).Select(async _ =>
		{
			while (true)
			{
				await WaitMeAsync();
				if (!TryTakeLast(inputs, out var payload)) continue;
				var currentRead = Interlocked.Increment(ref read);
				if (currentRead > Count) break;
				Bag.Add(payload);
			}
		});

		await Task.WhenAll(enqueue.Concat(reading).ToArray());
		ThrowIfUnMatched();
	}

	[Benchmark(Baseline = true)]
	public async Task ListWithSemaphore()
	{
		var inputs = new List<Payload>();
		var semaphore = new SemaphoreSlim(1, 1);
		var enqueue = Enumerable.Range(0, Count).Select(async _ =>
		{
			await WaitMeAsync();
			await semaphore.WaitAsync();
			try
			{
				inputs.Add(Payload.Default);
			}
			finally
			{
				semaphore.Release();
			}
		});

		var read = 0;
		var reading = Enumerable.Range(0, Count).Select(async _ =>
		{
			while (true)
			{
				await WaitMeAsync();
				await semaphore.WaitAsync();
				try
				{
					if (TryTakeLast(inputs, out var payload))
					{
						var current = Interlocked.Increment(ref read);
						if (current > Count) break;
						Bag.Add(payload);
					}
				}
				finally
				{
					semaphore.Release();
				}
			}
		});

		await Task.WhenAll(enqueue.Concat(reading).ToArray());
		ThrowIfUnMatched();
	}

	[Benchmark]
	public async Task ListWithAsyncReaderWriterLock()
	{
		var locker = new AsyncReaderWriterLock();
		var inputs = new List<Payload>();
		var canceller = new CancellationTokenSource();
		var enqueue = Enumerable.Range(0, Count).Select(async _ =>
		{
			await WaitMeAsync();
			using (await locker.EnterWriterScopeAsync(canceller.Token))
			{
				inputs.Add(Payload.Default);
			}
		});

		var read = 0;
		var reading = Enumerable.Range(0, Count).Select(async _ =>
		{
			while (true)
			{
				await WaitMeAsync();
				using (await locker.EnterReaderScopeAsync(canceller.Token))
				{
					if (TryTakeLast(inputs, out var payload))
					{
						var current = Interlocked.Increment(ref read);
						if (current > Count) break;
						Bag.Add(payload);
					}
				}
			}
		});

		await Task.WhenAll(enqueue.Concat(reading).ToArray());
		ThrowIfUnMatched();
		canceller.Dispose();
	}
}