using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using NExtensions.Benchmarking.LazyAsync;
using NExtensions.Benchmarking.ReadAndWriteLockAsync;
using Perfolizer.Horology;

namespace NExtensions.Benchmarking;

internal class Program
{
	private static async Task Main(string[] args)
	{
#if DEBUG
		var benchmark = new LockingBenchmarkUnlimited();
		await benchmark.ListWithAsyncReaderWriterLock();
		return;
#endif
		var config =
			ManualConfig.Create(DefaultConfig.Instance)
				.WithOption(ConfigOptions.DisableLogFile, true)
				.WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));
		
		//BenchmarkRunner.Run<LockingBenchmarkLimited>(config);
		//BenchmarkRunner.Run<LockingBenchmarkUnlimited>(config);
		BenchmarkRunner.Run<LazyBenchmark>(config);
		await ValueTask.CompletedTask;
	}
}