using BenchmarkDotNet.Attributes;
using NExtensions.Async;
using AsyncManualResetEventEx = Nito.AsyncEx.AsyncManualResetEvent;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

namespace NExtensions.Benchmarking.ManualResetEventAsync;

//[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class ManualResetEventBenchmarkDemo
{
	private ManualResetEvent? _are;
	private AsyncManualResetEventEx? _areEx;
	private AsyncManualResetEvent? _asyncAre;
	private ParallelOptions? _waitOptions;

	[Params(1, 10)]
	public int Waiters { get; set; } = 10;

	[Params(100_000)]
	public int Hits { get; set; } = 10_000;

	[IterationSetup]
	public void Setup()
	{
		_are = new ManualResetEvent(false);
		_areEx = new AsyncManualResetEventEx(false);
		_asyncAre = new AsyncManualResetEvent(false);
		_waitOptions = new ParallelOptions { MaxDegreeOfParallelism = Waiters };
	}

	[IterationCleanup]
	public void Cleanup()
	{
		_waitOptions = null;
		_are!.Dispose();
		_are = null;
		_areEx = null;
		_asyncAre!.Dispose();
		_asyncAre = null;
	}

	[Benchmark]
	public async Task ManualResetEvent()
	{
		var waits = 0;
		var waitAll = Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			await Task.Yield();
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) return;
				_are!.WaitOne();
			}
		});

		while (Volatile.Read(ref waits) < _waitOptions!.MaxDegreeOfParallelism)
			Thread.SpinWait(1);

		_are!.Set();
		await waitAll;
	}

	[Benchmark]
	public async Task AsyncExManualResetEvent()
	{
		var waits = 0;
		var waitAll = Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) return;
				await _areEx!.WaitAsync(CancellationToken.None);
			}
		});

		while (Volatile.Read(ref waits) < _waitOptions!.MaxDegreeOfParallelism)
			Thread.SpinWait(1);

		_areEx!.Set();
		await waitAll;
	}

	[Benchmark]
	public async Task AsyncManualResetEvent()
	{
		var waits = 0;
		var waitAll = Parallel.ForAsync(0, _waitOptions!.MaxDegreeOfParallelism, _waitOptions, async (_, _) =>
		{
			while (true)
			{
				var current = Interlocked.Increment(ref waits);
				if (current > Hits) return;
				await _asyncAre!.WaitAsync(CancellationToken.None);
			}
		});

		while (Volatile.Read(ref waits) < _waitOptions!.MaxDegreeOfParallelism)
			Thread.SpinWait(1);

		_asyncAre!.Set();
		await waitAll;
	}
}