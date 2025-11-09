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
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, warmupCount: 3)]
public class IdGenerationBench
    : BenchmarkBase
{
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

    [Benchmark(Description = "Id.GenerateNewGlobalId() (Global Monotonic)")]
    public PragmaStack.Core.Ids.Id GenerateNewGlobalId()
    {
        return PragmaStack.Core.Ids.Id.GenerateNewGlobalId();
    }
}
