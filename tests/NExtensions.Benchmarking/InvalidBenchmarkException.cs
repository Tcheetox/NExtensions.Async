namespace NExtensions.Benchmarking;

public class InvalidBenchmarkException : Exception
{
	public InvalidBenchmarkException(string message) : base(message)
	{
	}
}