using BenchmarkDotNet.Attributes;
using NExtensions.Async;
using AsyncEx = Nito.AsyncEx;

// ReSharper disable InconsistentNaming
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
namespace NExtensions.Benchmarking.LockAsync;

//[SimpleJob(warmupCount: 2, iterationCount: 8)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LockBenchmark
{
	[Params(1_000, 100_000)]
	public int Count { get; set; } = 10;

	[Params(1, 10)]
	public int Parallelism { get; set; } = 1;

	[Params("yield", "sync")]
	public string Wait { get; set; } = "delay";

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

	private void ThrowIfUnmatched(int count)
	{
		var expectedCount = Count * Parallelism;
		if (expectedCount != count)
			throw new InvalidBenchmarkException($"Invalid benchmark count, expected {count} to be {expectedCount}");
	}

	[Benchmark(Baseline = true)]
	public async Task Lock()
	{
		var sync = new object();

		var count = 0;
		if (Parallelism == 1)
		{
			for (var i = 0; i < Count; i++)
			{
				await WaitMeAsync().ConfigureAwait(false);
				lock (sync)
				{
					count++;
				}
			}

			ThrowIfUnmatched(count);
			return;
		}

		var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };
		await Parallel.ForAsync(0, Parallelism, options, async (_, _) =>
		{
			for (var i = 0; i < Count; i++)
			{
				await WaitMeAsync().ConfigureAwait(false);
				lock (sync)
				{
					count++;
				}
			}
		});
		ThrowIfUnmatched(count);
	}

	[Benchmark]
	public async Task AsyncExLock()
	{
		var sync = new AsyncEx.AsyncLock();

		var count = 0;
		if (Parallelism == 1)
		{
			for (var i = 0; i < Count; i++)
			{
				await WaitMeAsync();
				using (await sync.LockAsync())
				{
					count++;
				}
			}

			ThrowIfUnmatched(count);
			return;
		}

		var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };
		await Parallel.ForAsync(0, Parallelism, options, async (_, ct) =>
		{
			for (var i = 0; i < Count; i++)
			{
				await WaitMeAsync();
				using (await sync.LockAsync(ct))
				{
					count++;
				}
			}
		});
		ThrowIfUnmatched(count);
	}

	[Benchmark]
	public async Task AsyncLock()
	{
		var sync = new AsyncLock();

		var count = 0;
		if (Parallelism == 1)
		{
			for (var i = 0; i < Count; i++)
			{
				await WaitMeAsync();
				using (await sync.EnterScopeAsync())
				{
					count++;
				}
			}

			ThrowIfUnmatched(count);
			return;
		}

		var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };
		await Parallel.ForAsync(0, Parallelism, options, async (_, ct) =>
		{
			for (var i = 0; i < Count; i++)
			{
				await WaitMeAsync();
				using (await sync.EnterScopeAsync(ct))
				{
					count++;
				}
			}
		});
		ThrowIfUnmatched(count);
	}
}