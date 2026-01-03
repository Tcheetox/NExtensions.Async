using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

namespace NExtensions.UnitTests.Utilities;

internal static class ParallelUtility
{
	public static Task ForAsync(int fromInclusive, int toExclusive, Func<int, CancellationToken, ValueTask> body)
	{
#if NET8_0_OR_GREATER
		return Parallel.ForAsync(fromInclusive, toExclusive, body);
#else
		ArgumentNullException.ThrowIfNull(body);
		if (fromInclusive > toExclusive)
			throw new ArgumentOutOfRangeException(nameof(fromInclusive), $"'{nameof(fromInclusive)}' must be less than or equal to '{nameof(toExclusive)}'.");

		var range = Enumerable.Range(fromInclusive, toExclusive - fromInclusive);
		return Parallel.ForEachAsync(range, body);
#endif
	}

	public static Task ForAsync(int fromInclusive, int toExclusive, ParallelOptions options, Func<int, CancellationToken, ValueTask> body)
	{
#if NET8_0_OR_GREATER
		return Parallel.ForAsync(fromInclusive, toExclusive, options, body);
#else
		ArgumentNullException.ThrowIfNull(body);
		if (fromInclusive > toExclusive)
			throw new ArgumentOutOfRangeException(nameof(fromInclusive), $"'{nameof(fromInclusive)}' must be less than or equal to '{nameof(toExclusive)}'.");

		var range = Enumerable.Range(fromInclusive, toExclusive - fromInclusive);
		return Parallel.ForEachAsync(range, options, body);
#endif
	}

	public class SpanRecorder
	{
		private readonly int _hitsPerWindow;
		private readonly Func<int, int> _sleepFactory;

		private readonly ConcurrentQueue<long> _timestamps = new();

		private int _concurrentAccess;

		private int _hits;
		private int _maxConcurrentAccess;

		public SpanRecorder(int sleep = 0, int hitsPerWindow = 1)
			: this(_ => sleep, hitsPerWindow)
		{
		}

		public SpanRecorder(Func<int, int> sleepFactory, int hitsPerWindow = 1)
		{
			_sleepFactory = sleepFactory;
			_hitsPerWindow = hitsPerWindow;
		}

		[DebuggerNonUserCode]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public double[] TimestampsMs
		{
			get
			{
				if (_timestamps.IsEmpty)
					return Array.Empty<double>();

				var array = _timestamps.ToArray();
				var start = array[0];
				var freq = (double)Stopwatch.Frequency;
				return array.Select(t => (t - start) * 1000.0 / freq).ToArray();
			}
		}

		public int MaxConcurrentAccess => _maxConcurrentAccess;

		public TimeSpan Duration
		{
			get
			{
				var first = _timestamps.FirstOrDefault();
				var last = _timestamps.LastOrDefault();
				var durationInTicks = last - first;
				return StopwatchUtility.FromTicks(durationInTicks);
			}
		}

		public TimeSpan MinBetweenHits
		{
			get
			{
				var snapshot = _timestamps.ToArray();
				if (snapshot.Length <= _hitsPerWindow)
					return TimeSpan.Zero;

				var minDelta = long.MaxValue;
				for (var i = _hitsPerWindow; i < snapshot.Length; i += _hitsPerWindow)
				{
					var prevWindowStart = snapshot[i - _hitsPerWindow];
					var currentWindowStart = snapshot[i];
					var delta = currentWindowStart - prevWindowStart;

					if (delta < minDelta)
						minDelta = delta;
				}

				return StopwatchUtility.FromTicks(minDelta);
			}
		}

		public TimeSpan MaxBetweenHits
		{
			get
			{
				var snapshot = _timestamps.ToArray();
				if (snapshot.Length <= _hitsPerWindow)
					return TimeSpan.Zero;

				var maxDelta = long.MinValue;
				for (var i = _hitsPerWindow; i < snapshot.Length; i += _hitsPerWindow)
				{
					var prevWindowStart = snapshot[i - _hitsPerWindow];
					var currentWindowStart = snapshot[i];
					var delta = currentWindowStart - prevWindowStart;

					if (delta > maxDelta)
						maxDelta = delta;
				}

				return StopwatchUtility.FromTicks(maxDelta);
			}
		}

		public void Reset()
		{
			_timestamps.Clear();
		}

		public async Task SimulateAsync()
		{
			_timestamps.Enqueue(Stopwatch.GetTimestamp());

			var localCount = Interlocked.Increment(ref _concurrentAccess);
			InterlockedUtility.Max(ref _maxConcurrentAccess, localCount);

			var hit = Interlocked.Increment(ref _hits);
			var sleep = _sleepFactory(hit - 1);
			switch (sleep)
			{
				case 0:
					await Task.Yield();
					break;
				case > 0:
					await Task.Delay(sleep);
					break;
			}

			Interlocked.Decrement(ref _concurrentAccess);
		}
	}
}