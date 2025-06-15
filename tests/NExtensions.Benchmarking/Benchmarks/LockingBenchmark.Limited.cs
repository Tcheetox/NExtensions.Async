using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

// ReSharper disable InconsistentNaming

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
namespace NExtensions.Benchmarking.Benchmarks;

[SimpleJob(warmupCount: 2, iterationCount: 8)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LockingBenchmarkLimited : LockingBenchmark
{
	[Params("1/5", "1/10", "5/5", "5/10", "10/10", "10/5", "10/1", "5/1")]
	public string RW = "1/5";

	[Params(1_000_000)]
	public override int Count { get; set; } = 1_000_000;

	[Params("yield")]
	public override string Wait { get; set; } = "yield";

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (int Readers, int Writers) GetWorkersCount()
	{
		var split = RW.Split('/');
		return (int.Parse(split[0]), int.Parse(split[1]));
	}

	[Benchmark]
	public async Task ConcurrentQueue()
	{
		var queue = new ConcurrentQueue<Payload>();
		var enqueued = 0;
		var (readers, writers) = GetWorkersCount();
		var writeOptions = new ParallelOptions { MaxDegreeOfParallelism = writers };
		var enqueue = Parallel.ForAsync(0, writers, writeOptions, async (_, _) =>
		{
			while (true)
			{
				var item = Interlocked.Increment(ref enqueued);
				if (item > Count)
					break;

				await WaitMeAsync();
				queue.Enqueue(Payload.Default);
			}
		});

		var read = 0;
		var readOptions = new ParallelOptions { MaxDegreeOfParallelism = readers };
		var reading = Parallel.ForAsync(0, readers, readOptions, async (_, _) =>
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

		await Task.WhenAll(enqueue, reading);
		ThrowIfUnMatched();
	}

	[Benchmark]
	public async Task QueueWithSemaphore()
	{
		var queue = new Queue<Payload>();
		var semaphore = new SemaphoreSlim(1,1);
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
				await semaphore.WaitAsync(ct);
				try
				{
					queue.Enqueue(Payload.Default);
				}
				finally
				{
					semaphore.Release();
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
				await semaphore.WaitAsync(ct);
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

		await Task.WhenAll(enqueue, reading);
		ThrowIfUnMatched();
	}
	
	[Benchmark(Baseline = true)]
	public async Task Channelling()
	{
		var options = new UnboundedChannelOptions
		{
			SingleReader = false,
			SingleWriter = false
		};
		var channel = Channel.CreateUnbounded<Payload>(options);

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
				await channel.Writer.WriteAsync(Payload.Default, ct);
			}
		});
		
		var readOptions = new ParallelOptions { MaxDegreeOfParallelism = readers };
		var reading = Parallel.ForAsync(0, readers, readOptions, async (_, ct) =>
		{
			while (await channel.Reader.WaitToReadAsync(ct) && channel.Reader.TryRead(out var item))
			{
				await WaitMeAsync();
				Bag.Add(item);
			}
		});

		await enqueue;
		channel.Writer.Complete();
		await reading;
		ThrowIfUnMatched();
	}
}