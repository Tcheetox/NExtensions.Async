using NExtensions.Async;
using Shouldly;

namespace NExtensions.UnitTests.AsyncAutoResetEventTests;

public class GeneralTests
{
	// Dispose basics
// 	AutoResetEvent_Dispose_MultipleCalls_DoesNotThrow
// 		AutoResetEvent_Dispose_ReleasesNativeHandle
// 	AutoResetEvent_Dispose_MarksObjectAsDisposed
//
// // Post-dispose behavior
// 		AutoResetEvent_WaitOne_AfterDispose_ThrowsObjectDisposedException
// 	AutoResetEvent_Set_AfterDispose_ThrowsObjectDisposedException
// 		AutoResetEvent_Reset_AfterDispose_ThrowsObjectDisposedException
//
// // Interaction with finalization
// 	AutoResetEvent_Dispose_SuppressesFinalization
// 		AutoResetEvent_Finalizer_WhenNotDisposed_ReleasesHandle
//
// // Edge conditions
// 	AutoResetEvent_Dispose_DuringActiveWait_ThrowsOrUnblocks
// 		AutoResetEvent_Dispose_WhenNoNativeHandle_DoesNotThrow


	[Fact]
	public void Dispose_DoesNotThrow_ForMultipleCalls()
	{
		var autoResetEvent = new AsyncAutoResetEvent(true);
		
		autoResetEvent.Dispose();
		var act = () => autoResetEvent.Dispose();
		
		act.ShouldNotThrow();
	}
	
	[Fact]
	public void Set_ThrowsObjectDisposedException_WhenCalledAfterDispose()
	{
		var autoResetEvent = new AsyncAutoResetEvent(true);
		
		autoResetEvent.Dispose();
		var act = () => autoResetEvent.Set();
		
		act.ShouldThrow<ObjectDisposedException>();
	}
	
	[Fact]
	public void Reset_ThrowsObjectDisposedException_WhenCalledAfterDispose()
	{
		var autoResetEvent = new AsyncAutoResetEvent(true);
		
		autoResetEvent.Dispose();
		var act = () => autoResetEvent.Reset();
		
		act.ShouldThrow<ObjectDisposedException>();
	}
	
	[Fact]
	public async Task WaitAsync_ThrowsObjectDisposedException_WhenCalledAfterDispose()
	{
		var autoResetEvent = new AsyncAutoResetEvent(true);
		
		autoResetEvent.Dispose();
		var act = async () => await autoResetEvent.WaitAsync(CancellationToken.None);
		
		await act.ShouldThrowAsync<ObjectDisposedException>();
	}

	[Fact]
	public async Task WaitAsync_ThrowsSomethingn_WhenDisposedAfter()
	{
		var are = new SemaphoreSlim(0);
		Task.Run(async () =>
		{
			await Task.Delay(1000);
			are.Dispose();
		});
		try
		{
			await are.WaitAsync();
		}
		catch (Exception e)
		{
			var t = e;
		}
	

	// var autoResetEvent = new AsyncAutoResetEvent(true);
		//
		// autoResetEvent.Dispose();
		// var act = async () => await autoResetEvent.WaitAsync(CancellationToken.None);
		//
		// await act.ShouldThrowAsync<ObjectDisposedException>();
	}
}