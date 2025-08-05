using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NExtensions.Async.Collections;

/// <summary>
/// Represents a double-ended queue (deque) that allows efficient insertion and removal 
/// of elements at both ends using a circular buffer implementation.
/// </summary>
/// <typeparam name="T">The type of elements stored in the deque.</typeparam>
internal class Deque<T> : IEnumerable<T>
{
	private const int DefaultCapacity = 4;

	private readonly bool _deepClear;
	private T[] _buffer;
	private int _head;
	private int _tail;

	public Deque(int capacity = DefaultCapacity, bool? deepClear = null)
	{
		if (capacity < 0)
			throw new ArgumentOutOfRangeException(nameof(capacity), "Cannot be negative.");
		_buffer = new T[capacity];
		_deepClear = deepClear ?? RuntimeHelpers.IsReferenceOrContainsReferences<T>();
	}

	public int Count { get; private set; }

	public IEnumerator<T> GetEnumerator()
	{
		for (var i = 0; i < Count; i++)
		{
			yield return _buffer[Wrap(_head + i)];
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int Wrap(int index)
	{
		var bufferLength = _buffer.Length;
		if (index >= bufferLength) return index - bufferLength;
		if (index < 0) return index + bufferLength;
		return index;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfEmpty()
	{
		if (Count == 0)
			throw new InvalidOperationException("Deque is empty.");
	}

	public void AddLast(T item)
	{
		if (Count == _buffer.Length)
			Grow();

		_buffer[_tail] = item;
		_tail = Wrap(_tail + 1);
		Count++;
	}

	public void AddFirst(T item)
	{
		if (Count == _buffer.Length)
			Grow();

		_head = Wrap(_head - 1);
		_buffer[_head] = item;
		Count++;
	}

	public T RemoveFirst()
	{
		ThrowIfEmpty();
		var item = _buffer[_head];
		_buffer[_head] = default!;
		_head = Wrap(_head + 1);
		Count--;
		return item;
	}

	public bool TryRemoveFirst([NotNullWhen(true)] out T? item)
	{
		if (Count == 0)
		{
			item = default!;
			return false;
		}

		item = _buffer[_head]!;
		_buffer[_head] = default!;
		_head = Wrap(_head + 1);
		Count--;
		return true;
	}

	public bool Remove(T item)
	{
		var index = _head;
		var comparer = EqualityComparer<T>.Default;

		for (var i = 0; i < Count; i++)
		{
			if (comparer.Equals(_buffer[index], item))
			{
				RemoveAt(index);
				return true;
			}

			index = Wrap(index + 1);
		}

		return false;
	}

	private void RemoveAt(int index)
	{
		var bufferLength = _buffer.Length;

		var headToIndex = index - _head;
		if (headToIndex < 0)
			headToIndex += bufferLength;

		var indexToTail = _tail - index;
		if (indexToTail < 0)
			indexToTail += bufferLength;

		if (headToIndex < indexToTail)
		{
			for (var i = headToIndex; i > 0; i--)
			{
				var from = Wrap(_head + i - 1);
				var to = Wrap(_head + i);
				_buffer[to] = _buffer[from];
			}

			_buffer[_head] = default!;
			_head = Wrap(_head + 1);
		}
		else
		{
			for (var i = 0; i < indexToTail - 1; i++)
			{
				var from = Wrap(index + i + 1);
				var to = Wrap(index + i);
				_buffer[to] = _buffer[from];
			}

			_tail = Wrap(_tail - 1);
			_buffer[_tail] = default!;
		}

		Count--;
	}

	public void Clear()
	{
		if (_deepClear && Count > 0)
		{
			if (_head < _tail)
			{
				Array.Clear(_buffer, _head, Count);
			}
			else
			{
				Array.Clear(_buffer, _head, _buffer.Length - _head);
				Array.Clear(_buffer, 0, _tail);
			}
		}

		_head = 0;
		_tail = 0;
		Count = 0;
	}

	public void CopyTo(T[] array)
	{
		ArgumentNullException.ThrowIfNull(array);
		if (Count == 0)
			return; // Nothing to copy.

		if (array.Length < Count)
			throw new ArgumentException("Destination array is not large enough.", nameof(array));

		if (_head < _tail)
		{
			Array.Copy(_buffer, _head, array, 0, Count);
			return;
		}

		var rightCount = _buffer.Length - _head;
		Array.Copy(_buffer, _head, array, 0, rightCount);
		Array.Copy(_buffer, 0, array, rightCount, _tail);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Grow()
	{
		var newCapacity = _buffer.Length * 2;
		var newBuffer = new T[newCapacity];

		if (_head < _tail)
		{
			Array.Copy(_buffer, _head, newBuffer, 0, Count);
		}
		else
		{
			var rightCount = _buffer.Length - _head;
			Array.Copy(_buffer, _head, newBuffer, 0, rightCount);
			Array.Copy(_buffer, 0, newBuffer, rightCount, _tail);
		}

		_buffer = newBuffer;
		_head = 0;
		_tail = Count;
	}
}