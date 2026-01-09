using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NExtensions.Async;
using AsyncAutoResetEventEx = Nito.AsyncEx.AsyncAutoResetEvent;
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

namespace NExtensions.Benchmarking.AutoResetEventAsync;

//[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class AutoResetEventBenchmarkDemo
{
	[Params("3/1")]  //"1/3", "3/3", 
	public string SW { get; set; } = "1/3";
	
	[Params(150_000)]
	public int Hits { get; set; } = 10_000;

	//[Params("yield", "delay", "sync")]
	[Params("yield")]
	public string Wait { get; set; } = "yield";
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (int Set, int Wait) GetResetSetParallelism()
	{
		var split = SW.Split('/');
		return (int.Parse(split[0]), int.Parse(split[1]));
	}

	private AutoResetEvent? _are;
	private AsyncAutoResetEventEx? _areEx;
	private AsyncAutoResetEvent? _asyncAre;
	private CancellationTokenSource? _canceller;
	private ParallelOptions? _setOptions;
	private ParallelOptions? _waitOptions;
	
	[IterationSetup]
	public void SetupAutoResetEvent()
	{
		_are = new AutoResetEvent(false);
		_areEx = new AsyncAutoResetEventEx(false);
		_asyncAre = new AsyncAutoResetEvent(false);
		var (setParallelism, waitParallelism) = GetResetSetParallelism();
		_canceller = new CancellationTokenSource();
		_setOptions = new ParallelOptions { MaxDegreeOfParallelism = setParallelism, CancellationToken = _canceller.Token };
		_waitOptions = new ParallelOptions { MaxDegreeOfParallelism = waitParallelism };
	}

	[IterationCleanup]
	public void CleanupAutoResetEvent()
	{
		_canceller!.Cancel();
		_canceller = null;
		_setOptions = null;
		_waitOptions = null;
		_are!.Dispose();
		_are = null;
		_areEx = null;
		_asyncAre!.Dispose();
		_asyncAre = null;
	}

	
	[Benchmark]
	public async Task AutoResetEvent()
	{
		_ = Parallel.ForAsync(0, _setOptions!.MaxDegreeOfParallelism, _setOptions, async (_, ct) =>
		{
			while (!ct.IsCancellationRequested)
			{
				_are!.Set();
				await Utility.WaitMeAsync(Wait);
			}
		});
		
		var waits = 0;
		await Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) break;
	
				_are!.WaitOne();
				await Utility.WaitMeAsync(Wait);
			}
		});
	}
	
	[Benchmark]
	public async Task AsyncExAutoResetEvent()
	{
		_ = Parallel.ForAsync(0, _setOptions!.MaxDegreeOfParallelism, _setOptions, async (_, ct) =>
		{
			while (!ct.IsCancellationRequested)
			{
				_areEx!.Set();
				await Utility.WaitMeAsync(Wait);
			}
		});
		
		var waits = 0;
		await Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) break;
	
				await _areEx!.WaitAsync(CancellationToken.None);
				await Utility.WaitMeAsync(Wait);
			}
		});
	}
	
	[Benchmark]
	public async Task AsyncAutoResetEvent()
	{
		_ = Parallel.ForAsync(0, _setOptions!.MaxDegreeOfParallelism, _setOptions, async (_, ct) =>
		{
			while (!ct.IsCancellationRequested)
			{
				_asyncAre!.Set();
				await Utility.WaitMeAsync(Wait);
			}
		});
		
		var waits = 0;
		await Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) break;
	
				await _asyncAre!.WaitAsync(CancellationToken.None);
				await Utility.WaitMeAsync(Wait);
			}
		});
	}
}