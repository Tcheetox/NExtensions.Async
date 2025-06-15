using System.Collections.Concurrent;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

namespace NExtensions.Benchmarking.Benchmarks;

[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LockingBenchmarkUnlimited : LockingBenchmark
{
	[Benchmark]
	public async Task ConcurrentQueue_Unlimited()
	{
		var queue = new ConcurrentQueue<Payload>();
		var enqueue = Enumerable.Range(0, Count).Select(async _ =>
		{
			await WaitMeAsync();
			queue.Enqueue(Payload.Default);
		});
		var read = 0;
		var reading = Enumerable.Range(0, Count).Select(async _ =>
		{
			while (read < Count)
			{
				await WaitMeAsync();
				if (queue.TryDequeue(out var payload))
				{
					Interlocked.Increment(ref read);
					Bag.Add(payload);
				}
			}
		});

		await Task.WhenAll(enqueue.Concat(reading).ToArray());

		ThrowIfUnMatched();
	}

	[Benchmark]
	public async Task QueueWithSemaphore_Unlimited()
	{
		var queue = new Queue<Payload>();
		var semaphore = new SemaphoreSlim(1, 1);
		var enqueue = Enumerable.Range(0, Count).Select(async _ =>
		{
			await WaitMeAsync();
			await semaphore.WaitAsync();
			try
			{
				queue.Enqueue(Payload.Default);
			}
			finally
			{
				semaphore.Release();
			}
		});

		var read = 0;
		var reading = Enumerable.Range(0, Count).Select(async _ =>
		{
			while (read < Count)
			{
				await WaitMeAsync();
				await semaphore.WaitAsync();
				try
				{
					if (queue.TryDequeue(out var payload))
					{
						Interlocked.Increment(ref read);
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

	[Benchmark(Baseline = true)]
	public async Task Channelling_Unlimited()
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
}