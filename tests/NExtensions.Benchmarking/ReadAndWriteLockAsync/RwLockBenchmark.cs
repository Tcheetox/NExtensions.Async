using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;

// ReSharper disable MemberCanBeProtected.Global
namespace NExtensions.Benchmarking.ReadAndWriteLockAsync;

public abstract class RwLockBenchmark
{
	protected ConcurrentBag<Payload> Bag = [];

	[Params(100, 10_000)]
	public virtual int Count { get; set; } = 10_000;

	[Params("yield", "delay", "sync")]
	public virtual string Wait { get; set; } = "delay";

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

	protected static bool TryTakeLast<T>(IEnumerable<T> enumerable, [NotNullWhen(true)] out T? item) where T : class
	{
		item = null;
		foreach (var entry in enumerable)
			item = entry;
		return item is not null;
	}

	protected static bool TryTakeOne<T>(IEnumerable<T> enumerable, [NotNullWhen(true)] out T? item) where T : class
	{
		item = null;
		foreach (var entry in enumerable)
		{
			item = entry;
			// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			if (item is not null)
				return true;
		}

		return item is not null;
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

	protected sealed class Payload
	{
		public static readonly Payload Default = new(-1);

		// ReSharper disable once NotAccessedField.Local
		public readonly int Value;

		// ReSharper disable once MemberCanBePrivate.Global
		public Payload(int value)
		{
			Value = value;
		}
	}
}