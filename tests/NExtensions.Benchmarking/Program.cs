using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using NExtensions.Benchmarking.LockAsync;
using Perfolizer.Horology;

namespace NExtensions.Benchmarking;

internal class Program
{
	private static async Task Main(string[] args)
	{
#if DEBUG
		return;
#endif
		var config =
			ManualConfig.Create(DefaultConfig.Instance)
				//.AddLogger(ConsoleLogger.Default)
				.WithOption(ConfigOptions.DisableLogFile, true)
				.WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

		//BenchmarkRunner.Run<DequeBenchmark>(config);
		//BenchmarkRunner.Run<RwLockBenchmarkDemo>(config);
		//	BenchmarkRunner.Run<LazyBenchmark>(config);
		//	BenchmarkRunner.Run<LockBenchmark>(config);
		BenchmarkRunner.Run<LockBenchmarkDemo>(config);
		await ValueTask.CompletedTask;
	}
}