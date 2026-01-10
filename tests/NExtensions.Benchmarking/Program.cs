using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using NExtensions.Async;
using NExtensions.Benchmarking.AutoResetEventAsync;
using Perfolizer.Horology;

namespace NExtensions.Benchmarking;

internal class Program
{
	private static async Task Main(string[] args)
	{
#if DEBUG

		var tt = new AsyncAutoResetEvent(false);


		var temp = new AutoResetEventBenchmarkDemo();
		temp.SetupAutoResetEvent();
		await temp.AutoResetEvent();


		return;
#endif
		var config =
			ManualConfig.Create(DefaultConfig.Instance)
				//.AddLogger(ConsoleLogger.Default)
				.WithOption(ConfigOptions.DisableLogFile, true)
				.WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

		BenchmarkRunner.Run<AutoResetEventBenchmarkDemo>(config);
		//BenchmarkRunner.Run<AutoResetEventBenchmark>(config);

		//BenchmarkRunner.Run<DequeBenchmark>(config);
		// BenchmarkRunner.Run<RwLockBenchmarkDemo>(config);
		// BenchmarkRunner.Run<LazyBenchmarkDemo>(config);
		// BenchmarkRunner.Run<LockBenchmarkDemo>(config);

		await Task.CompletedTask;
	}
}