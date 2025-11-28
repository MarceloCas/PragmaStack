namespace Benchmarks.Benchs.IdBenchs;

/// <summary>
/// Benchmark para comparar diferentes abordagens de geração de IDs sequenciais.
///
/// Princípios aplicados:
/// - Baseline com Guid.CreateVersion7() nativo para comparação
/// - GlobalSetup para inicialização se necessário
/// - MemoryDiagnoser para rastrear alocações
/// - ThreadingDiagnoser para análise de concorrência
/// - Testes single-thread e multi-thread
/// - Comparação do overhead do TimeProvider
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, warmupCount: 3)]
public class IdGenerationBench
    : BenchmarkBase
{
    private PragmaStack.Core.TimeProviders.CustomTimeProvider _customTimeProvider = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _customTimeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: null,
            localTimeZone: null
        );
    }

    [Benchmark(Baseline = true, Description = "Guid.NewGuid() (Baseline Nativo - Guid V4)")]
    public Guid GuidCreateVersion4()
    {
        return Guid.NewGuid();
    }

    [Benchmark(Description = "Guid.CreateVersion7()")]
    public Guid GuidCreateVersion7()
    {
        return Guid.CreateVersion7();
    }

    [Benchmark(Description = "Id.GenerateNewId() (Per-Thread Monotonic)")]
    public PragmaStack.Core.Ids.Id GenerateNewId_PerThread()
    {
        return PragmaStack.Core.Ids.Id.GenerateNewId();
    }

    [Benchmark(Description = "Id.GenerateNewId(TimeProvider.System)")]
    public PragmaStack.Core.Ids.Id GenerateNewId_WithSystemTimeProvider()
    {
        return PragmaStack.Core.Ids.Id.GenerateNewId(TimeProvider.System);
    }

    [Benchmark(Description = "Id.GenerateNewId(CustomTimeProvider)")]
    public PragmaStack.Core.Ids.Id GenerateNewId_WithCustomTimeProvider()
    {
        return PragmaStack.Core.Ids.Id.GenerateNewId(_customTimeProvider);
    }

    [Benchmark(Description = "Id.GenerateNewId(DateTimeOffset)")]
    public PragmaStack.Core.Ids.Id GenerateNewId_WithDateTimeOffset()
    {
        return PragmaStack.Core.Ids.Id.GenerateNewId(DateTimeOffset.UtcNow);
    }
}
