namespace Benchmarks.Benchs.IdBenchs;

/// <summary>
/// Benchmark para testar geração de IDs em lote (batch), simulando alta carga.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, warmupCount: 3)]
public class IdGenerationBatchBench
    : BenchmarkBase
{
    [Params(10, 100, 1000, 10000)]
    public int BatchSize { get; set; }

    [Benchmark(Baseline = true, Description = "Guid.NewGuid() em lote (Guid V4)")]
    public Guid GuidCreateVersion4_Batch()
    {
        Guid lastResult = Guid.Empty;

        for (int i = 0; i < BatchSize; i++)
        {
            lastResult = Guid.NewGuid();
        }

        return lastResult;
    }

    [Benchmark(Description = "Guid.CreateVersion7() em lote")]
    public Guid GuidCreateVersion7_Batch()
    {
        Guid lastResult = Guid.Empty;

        for (int i = 0; i < BatchSize; i++)
        {
            lastResult = Guid.CreateVersion7();
        }

        return lastResult;
    }

    [Benchmark(Description = "Id.GenerateNewId() em lote")]
    public Guid GenerateNewId_Batch()
    {
        Guid lastResult = Guid.Empty;

        for (int i = 0; i < BatchSize; i++)
        {
            lastResult = PragmaStack.Core.Ids.Id.GenerateNewId();
        }

        return lastResult;
    }
}
