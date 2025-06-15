using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using NExtensions.Async;

namespace NExtensions.Benchmarking.Benchmarks;

[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LockingBenchmarkUnlimited : LockingBenchmark
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
	public async Task Channels()
	{
		var options = new UnboundedChannelOptions
		{
			SingleReader = false,
			SingleWriter = false
		};
		var channel = Channel.CreateUnbounded<Payload>(options);
		var producers = Enumerable.Range(0, Count).Select(async _ =>
		{
			await WaitMeAsync();
			await channel.Writer.WriteAsync(Payload.Default);
		}).ToArray();
	
		var consumers = Enumerable.Range(0, Count).Select(async _ =>
		{
			while (await channel.Reader.WaitToReadAsync() && channel.Reader.TryRead(out var item))
			{
				await WaitMeAsync();
				Bag.Add(item);
			}
		}).ToArray();
	
		await Task.WhenAll(producers);
		channel.Writer.Complete();
		await Task.WhenAll(consumers);
		ThrowIfUnMatched();
	}

	[Benchmark]
	public async Task ListWithAsyncReaderWriterLock()
	{
		var locker = new AsyncReaderWriterLock();
		var inputs = new List<Payload>();
		var enqueue = Enumerable.Range(0, Count).Select(async _ =>
		{
			await WaitMeAsync();
			using (await locker.WriterLockAsync())
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
				using (await locker.ReaderLockAsync())
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
	}
}