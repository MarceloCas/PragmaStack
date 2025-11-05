namespace Benchmarks.Benchs.TimeProvidersBenchs;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class CustomTimeProviderBench
    : BenchmarkBase
{
    private static readonly DateTimeOffset _dateTimeOffsetFixed = DateTimeOffset.UtcNow;

    [Params(1, 5)]
    public int IterationCount { get; set; }

    [Benchmark(Baseline = true, Description = "From DateTimeOffSet.UtcNow")]
    public DateTimeOffset GetUtcNow_From_DateTimeOffSet_UtcNow()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = DateTimeOffset.UtcNow;
        }

        return lastResult;
    }

    [Benchmark(Description = "From CustomTimeProvider without Func")]
    public DateTimeOffset GetUtcNow_From_CustomTimeProvider_Without_Func()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: null, localTimeZone: null);

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = timeProvider.GetUtcNow();
        }

        return lastResult;
    }

    [Benchmark(Description = "From CustomTimeProvider with Func and fixed value")]
    public DateTimeOffset GetUtcNow_From_CustomTimeProvider_With_Func_and_Fixed_Value()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        Func<TimeZoneInfo?, DateTimeOffset> utcNowFunc = (tz) => _dateTimeOffsetFixed;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: null, localTimeZone: null);

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = timeProvider.GetUtcNow();
        }

        return lastResult;
    }

    [Benchmark(Description = "From CustomTimeProvider with Func and dynamic value")]
    public DateTimeOffset GetUtcNow_From_CustomTimeProvider_With_Func_and_Dynamic_Value()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        Func<TimeZoneInfo?, DateTimeOffset> utcNowFunc = (tz) => DateTimeOffset.UtcNow;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: null, localTimeZone: null);

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = timeProvider.GetUtcNow();
        }

        return lastResult;
    }
}
