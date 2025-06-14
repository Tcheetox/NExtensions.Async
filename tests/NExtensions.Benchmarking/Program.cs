using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using NExtensions.Benchmarking.Benchmarks;
using Perfolizer.Horology;

namespace NExtensions.Benchmarking;

class Program
{
	static async Task Main(string[] args)
	{
		#if DEBUG
			var benchmark = new LockingBenchmarkUnlimited();
			await benchmark.Channel_Unlimited();
			return;
		#endif
		
		var config = 
			ManualConfig.Create(DefaultConfig.Instance)
			.WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));
		BenchmarkRunner.Run<LockingBenchmarkUnlimited>(config);
	}
}