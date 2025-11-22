namespace Benchmarks.Benchs.RegistryVersionBenchs;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, warmupCount: 3)]
public class RegistryVersionBench
    : BenchmarkBase
{
    private PragmaStack.Core.TimeProviders.CustomTimeProvider _customTimeProvider = null!;
    private DateTimeOffset _fixedTimestamp;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixedTimestamp = new DateTimeOffset(2025, 1, 16, 10, 30, 0, TimeSpan.Zero);
        
        _customTimeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: null,
            localTimeZone: null
        );
    }

    [Benchmark(Baseline = true, Description = "DateTime.UtcNow.Ticks (Baseline Nativo)")]
    public long DateTimeUtcNowTicks()
    {
        return DateTime.UtcNow.Ticks;
    }

    [Benchmark(Description = "RegistryVersion.GenerateNewVersion() (Per-Thread Monotonic)")]
    public PragmaStack.Core.RegistryVersions.RegistryVersion GenerateNewVersion_PerThread()
    {
        return PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
    }

    [Benchmark(Description = "RegistryVersion.GenerateNewVersion(TimeProvider.System)")]
    public PragmaStack.Core.RegistryVersions.RegistryVersion GenerateNewVersion_WithSystemTimeProvider()
    {
        return PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(TimeProvider.System);
    }

    [Benchmark(Description = "RegistryVersion.GenerateNewVersion(CustomTimeProvider)")]
    public PragmaStack.Core.RegistryVersions.RegistryVersion GenerateNewVersion_WithCustomTimeProvider()
    {
        return PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(_customTimeProvider);
    }

    [Benchmark(Description = "RegistryVersion.GenerateNewVersion(DateTimeOffset)")]
    public PragmaStack.Core.RegistryVersions.RegistryVersion GenerateNewVersion_WithDateTimeOffset()
    {
        return PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(DateTimeOffset.UtcNow);
    }

    [Benchmark(Description = "RegistryVersion.GenerateNewVersion(FixedTimestamp)")]
    public PragmaStack.Core.RegistryVersions.RegistryVersion GenerateNewVersion_WithFixedTimestamp()
    {
        return PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(_fixedTimestamp);
    }

    [Benchmark(Description = "RegistryVersion.FromLong(ticks)")]
    public PragmaStack.Core.RegistryVersions.RegistryVersion FromLong()
    {
        return PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(DateTime.UtcNow.Ticks);
    }

    [Benchmark(Description = "RegistryVersion.FromDateTimeOffset(DateTimeOffset.UtcNow)")]
    public PragmaStack.Core.RegistryVersions.RegistryVersion FromDateTimeOffset()
    {
        return PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(DateTimeOffset.UtcNow);
    }
}
