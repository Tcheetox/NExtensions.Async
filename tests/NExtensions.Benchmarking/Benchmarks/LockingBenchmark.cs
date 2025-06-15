using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;

// ReSharper disable MemberCanBeProtected.Global
namespace NExtensions.Benchmarking.Benchmarks;

public abstract class LockingBenchmark
{
	protected ConcurrentBag<Payload> Bag = null!;

	[Params(100, 10_000)]
	public virtual int Count { get; set; } = 10;

	[Params("yield", "delay", "sync")]
	public virtual string Wait { get; set; } = "yield";

	[IterationSetup]
	public void IterationSetup()
	{
		Bag = [];
	}

	protected void ThrowIfUnMatched()
	{
		if (Bag.Count != Count)
			throw new InvalidBenchmarkException($"Invalid benchmark count, expected {Bag.Count} to be {Count}");
	}

	protected async Task WaitMeAsync()
	{
		switch (Wait)
		{
			case "yield":
				await Task.Yield();
				return;
			case "delay":
				await Task.Delay(1);
				return;
			case "sync":
				return;
			default:
				throw new NotImplementedException($"Waiting for {Wait} is not implemented");
		}
	}

	public class InvalidBenchmarkException : Exception
	{
		public InvalidBenchmarkException(string message) : base(message)
		{
		}
	}

	protected sealed class Payload
	{
		public static readonly Payload Default = new(-1);

		// ReSharper disable once NotAccessedField.Local
		public readonly int Value;

		public Payload(int value)
		{
			Value = value;
		}
	}
}