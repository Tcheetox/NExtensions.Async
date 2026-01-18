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
	private AsyncEx.AsyncLock _asyncExLock = null!;
	private AsyncLock _asyncLock = null!;
	private ParallelOptions _options = null!;

	private SemaphoreSlim _semaphoreSlim = null!;

	[Params(150_000)]
	public int Hits { get; set; } = 100_000;

	[Params(1, 20)]
	public int Parallelism { get; set; } = 2;

	[Params("yield")]
	public string Wait { get; set; } = "yield";

	[GlobalSetup]
	public void Setup()
	{
		_semaphoreSlim = new SemaphoreSlim(1, 1);
		_asyncExLock = new AsyncEx.AsyncLock();
		_asyncLock = new AsyncLock();
		_options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_semaphoreSlim.Dispose();
		_semaphoreSlim = null!;
		_asyncExLock = null!;
		_asyncLock = null!;
		_options = null!;
	}

	[Benchmark]
	public async Task SemaphoreSlim()
	{
		var sync = _semaphoreSlim;

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

		await Parallel.ForAsync(0, Parallelism, _options, async (_, ct) =>
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
		var sync = _asyncExLock;

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

		await Parallel.ForAsync(0, Parallelism, _options, async (_, ct) =>
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
		var sync = _asyncLock;

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

		await Parallel.ForAsync(0, Parallelism, _options, async (_, ct) =>
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