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
public class LockBenchmarkDemo
{
	[Params(150_000)]
	public int Hits { get; set; } = 100_000;

	[Params(1, 20)]
	public int Parallelism { get; set; } = 2;

	[Params("yield")]
	public string Wait { get; set; } = "yield";


	[Benchmark]
	public async Task SemaphoreSlim()
	{
		var sync = new SemaphoreSlim(1, 1);

		if (Parallelism == 1)
		{
			for (var i = 0; i < Hits; i++)
			{
				try
				{
					await sync.WaitAsync();
					await Utility.WaitMeAsync(Wait);
				}
				finally
				{
					sync.Release();
				}
			}

			return;
		}

		var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };
		await Parallel.ForAsync(0, Parallelism, options, async (_, ct) =>
		{
			for (var i = 0; i < Hits; i++)
			{
				try
				{
					await sync.WaitAsync(ct);
					await Utility.WaitMeAsync(Wait);
				}
				finally
				{
					sync.Release();
				}
			}
		});
	}

	[Benchmark]
	public async Task AsyncExLock()
	{
		var sync = new AsyncEx.AsyncLock();

		if (Parallelism == 1)
		{
			for (var i = 0; i < Hits; i++)
			{
				using (await sync.LockAsync())
				{
					await Utility.WaitMeAsync(Wait);
				}
			}

			return;
		}

		var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };
		await Parallel.ForAsync(0, Parallelism, options, async (_, ct) =>
		{
			for (var i = 0; i < Hits; i++)
			{
				using (await sync.LockAsync(ct))
				{
					await Utility.WaitMeAsync(Wait);
				}
			}
		});
	}

	[Benchmark]
	public async Task AsyncLock()
	{
		var sync = new AsyncLock();

		if (Parallelism == 1)
		{
			for (var i = 0; i < Hits; i++)
			{
				using (await sync.EnterScopeAsync())
				{
					await Utility.WaitMeAsync(Wait);
				}
			}

			return;
		}

		var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };
		await Parallel.ForAsync(0, Parallelism, options, async (_, ct) =>
		{
			for (var i = 0; i < Hits; i++)
			{
				using (await sync.EnterScopeAsync(ct))
				{
					await Utility.WaitMeAsync(Wait);
				}
			}
		});
	}
}