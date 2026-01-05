using Shouldly;

namespace NExtensions.UnitTests.AsyncAutoResetEventTests;

public class AcquisitionTests
{
	[Fact]
	public async Task Set_DoNotStoreSignalsMoreThanOnce()
	{
		var resetEvent = new AutoResetEvent(true);
		var one = resetEvent.Set();
		var two = resetEvent.Set();
		var releasedCount = 0;

		var t1Got = false; 
		var t2Got = false;
		var t1 = Task.Run(() =>
		{
			t1Got = resetEvent.WaitOne();
			Interlocked.Increment(ref releasedCount);
		});
		var t2 = Task.Run(() =>
		{
			t2Got = resetEvent.WaitOne();
			Interlocked.Increment(ref releasedCount);
		});

		await Task.Delay(50);
		releasedCount.ShouldBe(1);
		one.ShouldBeTrue();
		two.ShouldBeTrue();
		if (t1.IsCompleted)
		{
			t1Got.ShouldBeTrue();
			t2Got.ShouldBeFalse();
			t2.IsCompleted.ShouldBeFalse();
		}
		else
		{
			t1Got.ShouldBeFalse();
			t2Got.ShouldBeTrue();
			t2.IsCompleted.ShouldBeTrue();
		}
	}
	
	[Fact]
	public async Task Set_ReleasesOneWaitingThread_EvenInParallelNTasks()
	{
		const int n = 30; 
		var resetEvent = new AutoResetEvent(false);
		var releasedCount = 0;
		
		var tasks = Enumerable.Range(0, n)
			.Select(_ => Task.Run(() =>
			{
				resetEvent.WaitOne();
				Interlocked.Increment(ref releasedCount);
			}))
			.ToArray();

		await Task.Delay(50);
		Parallel.For(0, n, _ => resetEvent.Set());
		
		await Task.WhenAll(tasks);
		releasedCount.ShouldBe(n);
	}
	
	[Fact]
	public async Task Set_ReleasesOneWaitingThread_EvenInParallel()
	{
		var resetEvent = new AutoResetEvent(false);
		var releasedCount = 0;
		
		var t1 = Task.Run(() =>
		{
			resetEvent.WaitOne();
			Interlocked.Increment(ref releasedCount);
		});
		var t2 = Task.Run(() =>
		{
			resetEvent.WaitOne();
			Interlocked.Increment(ref releasedCount);
		});
		var t3 = Task.Run(() =>
		{
			resetEvent.WaitOne();
			Interlocked.Increment(ref releasedCount);
		});
		
        await Task.Delay(50);
        Parallel.Invoke(
			() => resetEvent.Set(),
			() => resetEvent.Set(),
			() => resetEvent.Set()
		);
        
		await Task.WhenAll(t1, t2, t3);
		releasedCount.ShouldBe(3);
	}
}