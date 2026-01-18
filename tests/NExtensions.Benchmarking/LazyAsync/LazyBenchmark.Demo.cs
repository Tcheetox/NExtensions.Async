using BenchmarkDotNet.Attributes;
using NExtensions.Async;
using AsyncEx = Nito.AsyncEx;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace NExtensions.Benchmarking.LazyAsync;

//[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LazyBenchmarkDemo
{
	private ParallelOptions _options = null!;

	[Params(1, 10, 200)]
	public int Parallelism { get; set; } = 1;

	//[Params(0, 1)]
	public int Wait { get; set; } = 0;

	[GlobalSetup]
	public void Setup()
	{
		_options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };
	}

	private static async Task<int> GetAfterAsync(int after, CancellationToken token = default)
	{
		if (after == 0)
			await Task.Yield();
		await Task.Delay(after, token);
		return 1;
	}

	[Benchmark(Baseline = true)]
	public async Task Lazy_ExecutionAndPublication()
	{
		var lazy = new Lazy<Task<int>>(() => GetAfterAsync(Wait, CancellationToken.None));
		if (Parallelism == 1)
		{
			_ = await lazy.Value;
			return;
		}

		await Parallel.ForAsync(0, Parallelism, _options, async (_, _) => { _ = await lazy.Value; });
	}

	[Benchmark]
	public async Task AsyncExLazy_ExecutionAndPublication()
	{
		var lazy = new AsyncEx.AsyncLazy<int>(() => GetAfterAsync(Wait, CancellationToken.None));
		if (Parallelism == 1)
		{
			_ = await lazy;
			return;
		}

		await Parallel.ForAsync(0, Parallelism, _options, async (_, _) => { _ = await lazy; });
	}

	[Benchmark]
	public async Task AsyncLazy_ExecutionAndPublication()
	{
		var lazy = new AsyncLazy<int>(() => GetAfterAsync(Wait, CancellationToken.None));
		if (Parallelism == 1)
		{
			_ = await lazy;
			return;
		}

		await Parallel.ForAsync(0, Parallelism, _options, async (_, _) => { _ = await lazy; });
	}
}