using System.Collections.Concurrent;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable MemberCanBePrivate.Global

namespace NExtensions.Benchmarking.Benchmarks;

[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LockingBenchmarkUnlimited
{
	private class Payload
	{
		// ReSharper disable once NotAccessedField.Local
		public readonly int Value;
		public Payload(int value)
		{
			Value = value;
		}
	}
	
	[Params(100, 10_000)]
	public int Count = 10;

	[Params("yield", "delay", "sync")]
	public string Wait = "yield";
	
	private async Task WaitMeAsync()
	{
		switch (Wait)
		{
			case "yield":
				await Task.Yield();
				return;
			case "delay":
				await Task.Delay(1);
				return;
			case "sync":
				return;
			default:
				throw new NotImplementedException($"Waiting for {Wait} is not implemented");
		}
	}
	
	[Benchmark]
	public async Task ConcurrentQueue_Unlimited()
	{
		var queue = new ConcurrentQueue<Payload>();
		var bag = new ConcurrentBag<Payload>();
		var enqueue = Enumerable.Range(0, Count).Select(async i => 
		{
			await WaitMeAsync();
			queue.Enqueue(new Payload(i));
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
					bag.Add(payload);
				}
			}
		}); 
		
		await Task.WhenAll(enqueue.Concat(reading).ToArray());
		
		ThrowIfUnMatched(bag);
	}
	
	[Benchmark]
	public async Task QueueWithSemaphore_Unlimited()
	{
		var queue = new Queue<Payload>();
		var semaphore = new SemaphoreSlim(1, 1);
		var bag = new ConcurrentBag<Payload>();
		var enqueue = Enumerable.Range(0, Count).Select(async i =>
		{
			await WaitMeAsync();
			await semaphore.WaitAsync();
			try
			{
				queue.Enqueue(new Payload(i));
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
						bag.Add(payload);
					}
				}
				finally
				{
					semaphore.Release();
				}
			}
		});
		
		await Task.WhenAll(enqueue.Concat(reading).ToArray());
		ThrowIfUnMatched(bag);
	}

	[Benchmark(Baseline = true)]
	public async Task Channel_Unlimited()
	{
		var options = new UnboundedChannelOptions()
		{
			SingleReader = false,
			SingleWriter = false,
		};
		var channel = Channel.CreateUnbounded<Payload>(options);
		var bag = new ConcurrentBag<Payload>();
		
		var producers = Enumerable.Range(0, Count).Select(async i => 
		{
			await WaitMeAsync();
			await channel.Writer.WriteAsync(new Payload(i));
		}).ToArray();

		var consumers = Enumerable.Range(0, Count).Select(async _ => 
		{
			while (await channel.Reader.WaitToReadAsync() && channel.Reader.TryRead(out var item))
			{
				await WaitMeAsync();
				bag.Add(item);
			}
		}).ToArray();
		
		await Task.WhenAll(producers);
		channel.Writer.Complete();
		await Task.WhenAll(consumers);
		ThrowIfUnMatched(bag);
	}
	
	private void ThrowIfUnMatched(IEnumerable<Payload> payloads)
	{
		var count = payloads.Count();
		if (count != Count)
			throw new Exception($"Invalid benchmark count, expected {count} to be {Count}");
	}
}