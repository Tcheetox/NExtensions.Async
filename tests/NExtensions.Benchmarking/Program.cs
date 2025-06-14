using BenchmarkDotNet.Running;
using NExtensions.Benchmarking.Benchmarks;

namespace NExtensions.Benchmarking;

class Program
{
	static async Task Main(string[] args)
	{
		#if DEBUG
			var benchmark = new LockingBenchmark();
			await benchmark.ConcurrentQueue_Unlimited();
			return;
		#endif
		
		BenchmarkRunner.Run<LockingBenchmark>();
	}
}