using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using NExtensions.Benchmarking.AutoResetEventAsync;
using NExtensions.Benchmarking.ManualResetEventAsync;
using Perfolizer.Horology;

#pragma warning disable CS0162 // Unreachable code detected

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

		BenchmarkRunner.Run<ManualResetEventBenchmarkDemo>(config);
		BenchmarkRunner.Run<AutoResetEventBenchmarkDemo>(config);

		// BenchmarkRunner.Run<DequeBenchmark>(config);
		// BenchmarkRunner.Run<RwLockBenchmarkDemo>(config);
		// BenchmarkRunner.Run<LazyBenchmarkDemo>(config);
		// BenchmarkRunner.Run<LockBenchmarkDemo>(config);

		await Task.CompletedTask;
	}
}