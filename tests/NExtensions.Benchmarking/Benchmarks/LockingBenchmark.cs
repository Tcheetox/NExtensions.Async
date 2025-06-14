using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;

namespace NExtensions.Benchmarking.Benchmarks;

[SimpleJob(warmupCount: 2, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LockingBenchmark
{
	private class Payload
	{
		public readonly int Value;
		public Payload(int value)
		{
			Value = value;
		}
	}
	
	//[Params(1_000, 10_000)]
	[Params(10, 100, 1000)]
	public int Count = 10;
	
	[Benchmark(Baseline = true)]
	public async Task ConcurrentQueue_Unlimited()
	{
		var queue = new ConcurrentQueue<Payload>();
		var enqueue = Enumerable.Range(0, Count).Select(async i =>
		{
			await Task.Yield();
			queue.Enqueue(new Payload(i));
		}).ToArray(); // Prevent lazy initialization.
		
		var read = 0;
		var reading = Enumerable.Range(0, Count).Select(async _ =>
		{
			while (read < Count)
			{
				await Task.Yield();
				if (queue.TryDequeue(out var _))
					Interlocked.Increment(ref read);
			}
		}).ToArray(); // Ensure we start them possibly before all the enqueue tasks are done.
		
		await Task.WhenAll(enqueue.Concat(reading));
	}
}