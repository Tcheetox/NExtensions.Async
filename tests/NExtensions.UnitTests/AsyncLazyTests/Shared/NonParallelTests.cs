namespace NExtensions.UnitTests.AsyncLazyTests.Shared;

[CollectionDefinition("NonParallelTests", DisableParallelization = true)]
public class NonParallelTests
{
	public NonParallelTests()
	{
		CtorException.Reset();
		VoidResult.Reset();
	}
}