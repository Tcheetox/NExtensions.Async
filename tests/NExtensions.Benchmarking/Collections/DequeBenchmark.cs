using BenchmarkDotNet.Attributes;
using NExtensions.Async.Collections;

namespace NExtensions.Benchmarking.Collections;

// Adjust this if your namespace changes
using Deque = Deque<int>;
using DequeEx = Nito.Collections.Deque<int>;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class DequeBenchmark
{
	private DequeEx _dequeExForRemoveFirst = null!;
	private DequeEx _dequeExForRemoveMiddle = null!;

	private Deque _dequeForRemoveFirst = null!;
	private Deque _dequeForRemoveMiddle = null!;

	private Queue<int> _queueForRemoveFirst = null!;

	[Params(100, 1_000, 50_000)]
	public int Size { get; set; } = 10_000;

	[Benchmark]
	public void AddQueue()
	{
		var queue = new Queue<int>();
		for (var i = 0; i < Size; i++)
			queue.Enqueue(i);
	}

	[IterationSetup(Target = nameof(RemoveQueue))]
	public void SetupQueue()
	{
		_queueForRemoveFirst = new Queue<int>(Size);
		for (var i = 0; i < Size; i++)
			_queueForRemoveFirst.Enqueue(i);
	}

	[IterationSetup(Target = nameof(CopyQueue))]
	public void SetupCopyQueue()
	{
		SetupQueue();
	}

	[Benchmark]
	public void CopyQueue()
	{
		var array = new int[_queueForRemoveFirst.Count];
		_queueForRemoveFirst.CopyTo(array, 0);
	}

	[IterationSetup(Target = nameof(CopyDequeue))]
	public void SetupCopyDeque()
	{
		SetupDeque();
	}

	[Benchmark]
	public void CopyDequeue()
	{
		var array = new int[_dequeForRemoveFirst.Count];
		_dequeForRemoveFirst.CopyTo(array);
	}

	[Benchmark]
	public void RemoveQueue()
	{
		while (_queueForRemoveFirst.Count > 0)
			_queueForRemoveFirst.Dequeue();
	}

	[Benchmark]
	public void AddLast()
	{
		var deque = new Deque();
		for (var i = 0; i < Size; i++)
			deque.AddLast(i);
	}


	[Benchmark]
	public void AddLastEx()
	{
		var deque = new DequeEx();
		for (var i = 0; i < Size; i++)
			deque.AddToBack(i);
	}

	[IterationSetup(Target = nameof(RemoveFirst))]
	public void SetupDeque()
	{
		_dequeForRemoveFirst = new Deque(Size);
		for (var i = 0; i < Size; i++)
			_dequeForRemoveFirst.AddLast(i);
	}

	[IterationSetup(Target = nameof(RemoveFirstEx))]
	public void SetupDequeEx()
	{
		_dequeExForRemoveFirst = new DequeEx(Size);
		for (var i = 0; i < Size; i++)
			_dequeExForRemoveFirst.AddToBack(i);
	}

	[Benchmark]
	public void RemoveFirst()
	{
		while (_dequeForRemoveFirst.Count > 0)
			_dequeForRemoveFirst.RemoveFirst();
	}

	[Benchmark]
	public void RemoveFirstEx()
	{
		while (_dequeExForRemoveFirst.Count > 0)
			_dequeExForRemoveFirst.RemoveFromFront();
	}

	[IterationSetup(Target = nameof(RemoveMiddle))]
	public void SetupDequeForRemoveMiddle()
	{
		_dequeForRemoveMiddle = new Deque(Size);
		for (var i = 0; i < Size; i++)
			_dequeForRemoveMiddle.AddLast(i);
	}

	[IterationSetup(Target = nameof(RemoveMiddleEx))]
	public void SetupDequeExForRemoveMiddle()
	{
		_dequeExForRemoveMiddle = new DequeEx(Size);
		for (var i = 0; i < Size; i++)
			_dequeExForRemoveMiddle.AddToBack(i);
	}

	[Benchmark]
	public void RemoveMiddle()
	{
		_dequeForRemoveMiddle.Remove(Size / 2);
	}

	[Benchmark]
	public void RemoveMiddleEx()
	{
		_dequeExForRemoveMiddle.Remove(Size / 2);
	}
}