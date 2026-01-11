using System.Collections;
using NExtensions.Async.Collections;
using Shouldly;

namespace NExtensions.UnitTests.Collections;

public class DequeTests
{
	#region Constructor Tests

	[Fact]
	public void Constructor_CreatesEmptyDeque_WhenCalledWithDefaultParameters()
	{
		// Act
		var deque = new Deque<int>();

		// Assert
		deque.Count.ShouldBe(0);
		deque.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	public void Constructor_CreatesEmptyDequeWithSpecifiedCapacity_WhenValidCapacityProvided(int capacity)
	{
		// Act
		var deque = new Deque<int>(capacity);

		// Assert
		deque.Count.ShouldBe(0);
		deque.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_ThrowsArgumentOutOfRangeException_WhenNegativeCapacityProvided()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentOutOfRangeException>(() => new Deque<int>(-1));
		exception.ParamName.ShouldBe("capacity");
		exception.Message.ShouldContain("Cannot be negative.");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Constructor_AcceptsDeepClearParameter_WhenSpecified(bool deepClear)
	{
		// Act
		var deque = new Deque<string>(4, deepClear);

		// Assert
		deque.Count.ShouldBe(0);
	}

	#endregion

	#region AddLast Tests

	[Fact]
	public void AddLast_AddsItemToEmptyDeque_WhenDequeIsEmpty()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddLast(42);

		// Assert
		deque.Count.ShouldBe(1);
		deque.Single().ShouldBe(42);
	}

	[Fact]
	public void AddLast_AddsMultipleItems_WhenCalledSequentially()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddLast(3);

		// Assert
		deque.Count.ShouldBe(3);
		deque.ShouldBe(new[] { 1, 2, 3 });
	}

	[Fact]
	public void AddLast_ShouldGrowCapacity_WhenBufferIsFull()
	{
		// Arrange
		var deque = new Deque<int>(2);
		deque.AddLast(1);
		deque.AddLast(2);

		// Act
		deque.AddLast(3);

		// Assert
		deque.Count.ShouldBe(3);
		deque.ShouldBe(new[] { 1, 2, 3 });
	}

	[Fact]
	public void AddLast_ShouldHandleNullValues_WhenTypeIsNullable()
	{
		// Arrange
		var deque = new Deque<string?>();

		// Act
		deque.AddLast(null);
		deque.AddLast("test");

		// Assert
		deque.Count.ShouldBe(2);
		deque.ShouldBe(new[] { null, "test" });
	}

	#endregion

	#region AddFirst Tests

	[Fact]
	public void AddFirst_AddsItemToEmptyDeque_WhenDequeIsEmpty()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddFirst(42);

		// Assert
		deque.Count.ShouldBe(1);
		deque.Single().ShouldBe(42);
	}

	[Fact]
	public void AddFirst_AddsItemsInReverseOrder_WhenCalledSequentially()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddFirst(1);
		deque.AddFirst(2);
		deque.AddFirst(3);

		// Assert
		deque.Count.ShouldBe(3);
		deque.ShouldBe(new[] { 3, 2, 1 });
	}

	[Fact]
	public void AddFirst_ShouldGrowCapacity_WhenBufferIsFull()
	{
		// Arrange
		var deque = new Deque<int>(2);
		deque.AddFirst(1);
		deque.AddFirst(2);

		// Act
		deque.AddFirst(3);

		// Assert
		deque.Count.ShouldBe(3);
		deque.ShouldBe(new[] { 3, 2, 1 });
	}

	[Fact]
	public void AddFirst_WorkWithAddLast_WhenMixedOperations()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddLast(2);
		deque.AddFirst(1);
		deque.AddLast(3);
		deque.AddFirst(0);

		// Assert
		deque.Count.ShouldBe(4);
		deque.ShouldBe(new[] { 0, 1, 2, 3 });
	}

	#endregion

	#region RemoveFirst Tests

	[Fact]
	public void RemoveFirst_ThrowsInvalidOperationException_WhenDequeIsEmpty()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => deque.RemoveFirst());
		exception.Message.ShouldBe("Deque is empty.");
	}

	[Fact]
	public void RemoveFirst_RemoveFirstItem_WhenDequeHasOneItem()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(42);

		// Act
		var result = deque.RemoveFirst();

		// Assert
		result.ShouldBe(42);
		deque.Count.ShouldBe(0);
		deque.ShouldBeEmpty();
	}

	[Fact]
	public void RemoveFirst_RemoveFirstItem_WhenDequeHasMultipleItems()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddLast(3);

		// Act
		var result = deque.RemoveFirst();

		// Assert
		result.ShouldBe(1);
		deque.Count.ShouldBe(2);
		deque.ShouldBe(new[] { 2, 3 });
	}

	[Fact]
	public void RemoveFirst_HandleCircularBuffer_WhenHeadWrapsAround()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddFirst(0);
		deque.AddFirst(-1);

		// Act
		var result = deque.RemoveFirst();

		// Assert
		result.ShouldBe(-1);
		deque.ShouldBe(new[] { 0, 1, 2 });
	}

	#endregion

	#region TryRemoveFirst Tests

	[Fact]
	public void TryRemoveFirst_ReturnFalse_WhenDequeIsEmpty()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		var result = deque.TryRemoveFirst(out var item);

		// Assert
		result.ShouldBeFalse();
		item.ShouldBe(0);
	}

	[Fact]
	public void TryRemoveFirst_ReturnTrueAndItem_WhenDequeHasItems()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(42);

		// Act
		var result = deque.TryRemoveFirst(out var item);

		// Assert
		result.ShouldBeTrue();
		item.ShouldBe(42);
		deque.Count.ShouldBe(0);
	}

	[Fact]
	public void TryRemoveFirst_ReturnTrueAndFirstItem_WhenDequeHasMultipleItems()
	{
		// Arrange
		var deque = new Deque<string>();
		deque.AddLast("first");
		deque.AddLast("second");

		// Act
		var result = deque.TryRemoveFirst(out var item);

		// Assert
		result.ShouldBeTrue();
		item.ShouldBe("first");
		deque.Count.ShouldBe(1);
		deque.Single().ShouldBe("second");
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_ReturnFalse_WhenDequeIsEmpty()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		var result = deque.Remove(42);

		// Assert
		result.ShouldBeFalse();
		deque.Count.ShouldBe(0);
	}

	[Fact]
	public void Remove_ReturnFalse_WhenItemNotFound()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddLast(3);

		// Act
		var result = deque.Remove(4);

		// Assert
		result.ShouldBeFalse();
		deque.Count.ShouldBe(3);
		deque.ShouldBe(new[] { 1, 2, 3 });
	}

	[Fact]
	public void Remove_ReturnTrueAndRemoveFirstOccurrence_WhenItemExists()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddLast(2);
		deque.AddLast(3);

		// Act
		var result = deque.Remove(2);

		// Assert
		result.ShouldBeTrue();
		deque.Count.ShouldBe(3);
		deque.ShouldBe(new[] { 1, 2, 3 });
	}

	[Fact]
	public void Remove_ShouldShiftElementsFromTailSide_WhenTailIsSmallerThanHead()
	{
		// Arrange
		var deque = new Deque<string>(6);

		// Add elements: [A, B, C, D, E]
		deque.AddLast("A");
		deque.AddLast("B");
		deque.AddLast("C");
		deque.AddLast("D");
		deque.AddLast("E");
		var initialCount = deque.Count;

		// Act
		var removed = deque.Remove("D");

		// Assert
		removed.ShouldBeTrue();
		deque.Count.ShouldBe(initialCount - 1);

		// Verify the remaining elements are in correct order
		var elements = deque.ToArray();
		elements.ShouldBe(new[] { "A", "B", "C", "E" });
		deque.Remove("D").ShouldBeFalse();
	}

	[Fact]
	public void Remove_RemoveOnlyItem_WhenDequeHasOneItem()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(42);

		// Act
		var result = deque.Remove(42);

		// Assert
		result.ShouldBeTrue();
		deque.Count.ShouldBe(0);
		deque.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	[InlineData(4)]
	public void Remove_Should_RemoveItemAtAnyPosition_WhenItemExists(int indexToRemove)
	{
		// Arrange
		var deque = new Deque<int>();
		var items = new[] { 0, 1, 2, 3, 4 };
		foreach (var item in items)
		{
			deque.AddLast(item);
		}

		// Act
		var result = deque.Remove(indexToRemove);

		// Assert
		result.ShouldBeTrue();
		deque.Count.ShouldBe(4);
		deque.ShouldNotContain(indexToRemove);
	}

	[Fact]
	public void Remove_HandleNullValues_WhenTypeIsNullable()
	{
		// Arrange
		var deque = new Deque<string?>();
		deque.AddLast("test");
		deque.AddLast(null);
		deque.AddLast("another");

		// Act
		var result = deque.Remove(null);

		// Assert
		result.ShouldBeTrue();
		deque.Count.ShouldBe(2);
		deque.ShouldBe(new[] { "test", "another" });
	}

	#endregion

	#region Clear Tests

	[Fact]
	public void Clear_EmptyDeque_WhenDequeHasItems()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddLast(3);

		// Act
		deque.Clear();

		// Assert
		deque.Count.ShouldBe(0);
		deque.ShouldBeEmpty();
	}

	[Fact]
	public void Clear_DoNothing_WhenDequeIsEmpty()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.Clear();

		// Assert
		deque.Count.ShouldBe(0);
		deque.ShouldBeEmpty();
	}

	[Fact]
	public void Clear_AllowSubsequentOperations_WhenDequeIsCleared()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);

		// Act
		deque.Clear();
		deque.AddLast(42);

		// Assert
		deque.Count.ShouldBe(1);
		deque.Single().ShouldBe(42);
	}

	[Fact]
	public void Clear_ShouldClearAllElements_WhenHeadLessThanTailAndDeepClearTrue()
	{
		var deque = new Deque<string>(4, true);

		// Add elements to create scenario where head < tail and Count > 0
		deque.AddLast("item1");
		deque.AddLast("item2");
		deque.AddLast("item3");

		// Verify preconditions
		deque.Count.ShouldBeGreaterThan(0);

		// Act
		deque.Clear();

		// Assert
		deque.Count.ShouldBe(0);
		deque.AsEnumerable().ShouldBeEmpty();
		Should.Throw<InvalidOperationException>(() => deque.RemoveFirst());
	}

	#endregion

	#region CopyTo Tests

	[Fact]
	public void CopyTo_ThrowsArgumentNullException_WhenArrayIsNull()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => deque.CopyTo(null!));
	}

	[Fact]
	public void CopyTo_ThrowsArgumentException_WhenArrayIsTooSmall()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddLast(3);
		var array = new int[2];

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => deque.CopyTo(array));
		exception.ParamName.ShouldBe("array");
		exception.Message.ShouldContain("Destination array is not large enough.");
	}

	[Fact]
	public void CopyTo_CopyAllElements_WhenArrayIsSufficientSize()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddLast(3);
		var array = new int[5];

		// Act
		deque.CopyTo(array);

		// Assert
		array.Take(3).ShouldBe(new[] { 1, 2, 3 });
		array[3].ShouldBe(0); // Remaining elements should be default
		array[4].ShouldBe(0);
	}

	[Fact]
	public void CopyTo_CopyAllElementsInCorrectOrder_WhenDequeIsEmpty()
	{
		// Arrange
		var deque = new Deque<int>();
		var array = new int[3];

		// Act
		deque.CopyTo(array);

		// Assert
		array.ShouldAllBe(x => x == 0);
	}

	[Fact]
	public void CopyTo_HandleCircularBuffer_WhenElementsWrapAround()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddFirst(0);
		deque.AddFirst(-1);
		var array = new int[6];

		// Act
		deque.CopyTo(array);

		// Assert
		array.Take(4).ShouldBe(new[] { -1, 0, 1, 2 });
		array[4].ShouldBe(0);
		array[5].ShouldBe(0);
	}

	#endregion

	#region Enumeration Tests

	[Fact]
	public void GetEnumerator_ReturnEmptyEnumeration_WhenDequeIsEmpty()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act & Assert
		deque.ShouldBeEmpty();
	}

	[Fact]
	public void GetEnumerator_ReturnElementsInCorrectOrder_WhenDequeHasItems()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddFirst(0);

		// Act & Assert
		((IEnumerable)deque).ShouldBe(new[] { 0, 1, 2 });
		deque.ShouldBe(new[] { 0, 1, 2 });
	}

	[Fact]
	public void GetEnumerator_HandleCircularBuffer_WhenElementsWrapAround()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(2);
		deque.AddLast(3);
		deque.AddFirst(1);
		deque.AddFirst(0);

		// Act & Assert
		deque.ShouldBe(new[] { 0, 1, 2, 3 });
	}

	[Fact]
	public void GetEnumerator_ReturnCorrectElements_AfterRemoveOperations()
	{
		// Arrange
		var deque = new Deque<int>();
		deque.AddLast(1);
		deque.AddLast(2);
		deque.AddLast(3);
		deque.AddLast(4);
		deque.RemoveFirst();
		deque.Remove(3);

		// Act & Assert
		deque.ShouldBe(new[] { 2, 4 });
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void Deque_KeepsProperState_WhenMixingAllOperations()
	{
		// Arrange
		var deque = new Deque<string>(2);

		// Act & Assert - Build up deque
		deque.AddLast("middle");
		deque.AddFirst("start");
		deque.AddLast("end");
		deque.ShouldBe(new[] { "start", "middle", "end" });

		// Remove from front
		var first = deque.RemoveFirst();
		first.ShouldBe("start");
		deque.ShouldBe(new[] { "middle", "end" });

		// Add more items
		deque.AddFirst("new_start");
		deque.AddLast("new_end");
		deque.ShouldBe(new[] { "new_start", "middle", "end", "new_end" });

		// Remove specific item
		var removed = deque.Remove("middle");
		removed.ShouldBeTrue();
		deque.ShouldBe(new[] { "new_start", "end", "new_end" });

		// Try to remove first
		var tryResult = deque.TryRemoveFirst(out var item);
		tryResult.ShouldBeTrue();
		item.ShouldBe("new_start");
		deque.ShouldBe(new[] { "end", "new_end" });

		// Copy to array
		var array = new string[5];
		deque.CopyTo(array);
		array.Take(2).ShouldBe(new[] { "end", "new_end" });

		// Clear
		deque.Clear();
		deque.ShouldBeEmpty();
		deque.Count.ShouldBe(0);
	}

	[Fact]
	public void Deque_MaintainsCorrectOrder_WhenGrowingMultipleTimes()
	{
		// Arrange
		var deque = new Deque<int>(2);
		var expected = new List<int>();

		// Act - Add items that will cause multiple grows
		for (var i = 0; i < 20; i++)
		{
			if (i % 2 == 0)
			{
				deque.AddLast(i);
				expected.Add(i);
			}
			else
			{
				deque.AddFirst(i);
				expected.Insert(0, i);
			}
		}

		// Assert
		deque.Count.ShouldBe(20);
		deque.ShouldBe(expected);
	}

	#endregion
}