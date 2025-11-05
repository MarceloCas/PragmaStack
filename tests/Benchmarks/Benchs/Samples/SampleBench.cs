using PragmaStack.Core;
using System.Numerics;

namespace Benchmarks.Benchs.Samples;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class SampleBench
    : BenchmarkBase
{
    [Params(1, 10)]
    public int IterationCount { get; set; }

    [Benchmark(Description = "Sum integers", Baseline = true)]
    public INumber<int> SumIntegers()
    {
        INumber<int> lastResult = 0;

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = SampleClass.AddNumbers(i, i);
        }

        return lastResult;
    }

    [Benchmark(Description = "Sum longs")]
    public INumber<long> SumLongs()
    {
        INumber<long> lastResult = 0L;
        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = SampleClass.AddNumbers((long)i, (long)i);
        }
        return lastResult;
    }

    [Benchmark(Description = "Sum floats")]
    public INumber<float> SumFloats()
    {
        INumber<float> lastResult = 0f;
        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = SampleClass.AddNumbers((float)i, (float)i);
        }
        return lastResult;
    }

    [Benchmark(Description = "Sum doubles")]
    public INumber<double> SumDoubles()
    {
        INumber<double> lastResult = 0d;
        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = SampleClass.AddNumbers((double)i, (double)i);
        }
        return lastResult;
    }

    [Benchmark(Description = "Sum decimals")]
    public INumber<decimal> SumDecimals()
    {
        INumber<decimal> lastResult = 0m;
        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = SampleClass.AddNumbers((decimal)i, (decimal)i);
        }
        return lastResult;
    }
}
