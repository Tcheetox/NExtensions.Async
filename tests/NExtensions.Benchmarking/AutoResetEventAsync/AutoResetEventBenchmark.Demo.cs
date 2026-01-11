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
	private AutoResetEvent? _are;
	private AsyncAutoResetEventEx? _areEx;
	private AsyncAutoResetEvent? _asyncAre;
	private ParallelOptions? _setOptions;
	private ParallelOptions? _waitOptions;

	[Params("1/1", "1/10", "3/100")]
	public string SW { get; set; } = "1/10";

	[Params(100_000)]
	public int Hits { get; set; } = 10_000;

	[Params("yield")]
	public string Wait { get; set; } = "yield";

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (int Set, int Wait) GetResetSetParallelism()
	{
		var split = SW.Split('/');
		return (int.Parse(split[0]), int.Parse(split[1]));
	}

	[IterationSetup]
	public void SetupAutoResetEvent()
	{
		_are = new AutoResetEvent(false);
		_areEx = new AsyncAutoResetEventEx(false);
		_asyncAre = new AsyncAutoResetEvent(false);

		var (setParallelism, waitParallelism) = GetResetSetParallelism();
		_setOptions = new ParallelOptions { MaxDegreeOfParallelism = setParallelism };
		_waitOptions = new ParallelOptions { MaxDegreeOfParallelism = waitParallelism };
	}

	[IterationCleanup]
	public void CleanupAutoResetEvent()
	{
		_setOptions = null;
		_waitOptions = null;
		_are!.Dispose();
		_are = null;
		_areEx = null;
		_asyncAre!.Dispose();
		_asyncAre = null;
	}

	[Benchmark]
	public Task AutoResetEvent()
	{
		var waits = 0;
		var waitAll = Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) return;
				_are!.WaitOne();
				await Utility.WaitMeAsync(Wait);
			}
		});

		Parallel.For(0, _setOptions!.MaxDegreeOfParallelism, _setOptions, _ =>
		{
			while (!waitAll.IsCompletedSuccessfully)
			{
				_are!.Set();
				//Thread.SpinWait(1);
			}
		});
		return waitAll;
	}

	[Benchmark]
	public Task AsyncExAutoResetEvent()
	{
		var waits = 0;
		var waitAll = Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) return;
				await _areEx!.WaitAsync(CancellationToken.None);
				await Utility.WaitMeAsync(Wait);
			}
		});

		Parallel.For(0, _setOptions!.MaxDegreeOfParallelism, _setOptions, _ =>
		{
			while (!waitAll.IsCompletedSuccessfully)
			{
				_areEx!.Set();
				//Thread.SpinWait(1);
			}
		});
		return waitAll;
	}

	[Benchmark]
	public Task AsyncAutoResetEvent()
	{
		var waits = 0;
		var waitAll = Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) return;
				await _asyncAre!.WaitAsync(CancellationToken.None);
				await Utility.WaitMeAsync(Wait);
			}
		});

		Parallel.For(0, _setOptions!.MaxDegreeOfParallelism, _setOptions, _ =>
		{
			while (!waitAll.IsCompletedSuccessfully)
			{
				_asyncAre!.Set();
				//Thread.SpinWait(1);
			}
		});
		return waitAll;
	}
}