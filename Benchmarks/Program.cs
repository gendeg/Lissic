using BenchmarkDotNet.Running;

namespace Benchmarks;

internal class Program
{
    static void Main(string[] _)
    {
        BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}
