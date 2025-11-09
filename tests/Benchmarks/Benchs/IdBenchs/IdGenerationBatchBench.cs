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
    private List<Guid> _results;

    [Params(10, 100, 1000, 10000)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _results = new List<Guid>(BatchSize);
    }

    [Benchmark(Baseline = true, Description = "Guid.NewGuid() em lote (Guid V4)")]
    public void GuidCreateVersion4_Batch()
    {
        _results.Clear();

        for (int i = 0; i < BatchSize; i++)
        {
            _results.Add(Guid.NewGuid());
        }
    }

    [Benchmark(Description = "Guid.CreateVersion7() em lote")]
    public void GuidCreateVersion7_Batch()
    {
        _results.Clear();

        for (int i = 0; i < BatchSize; i++)
        {
            _results.Add(Guid.CreateVersion7());
        }
    }

    [Benchmark(Description = "Id.GenerateNewId() em lote")]
    public void GenerateNewId_Batch()
    {
        _results.Clear();

        for (int i = 0; i < BatchSize; i++)
        {
            _results.Add(PragmaStack.Core.Ids.Id.GenerateNewId());
        }
    }

    [Benchmark(Description = "Id.GenerateNewGlobalId() em lote")]
    public void GenerateNewGlobalId_Batch()
    {
        _results.Clear();

        for (int i = 0; i < BatchSize; i++)
        {
            _results.Add(PragmaStack.Core.Ids.Id.GenerateNewGlobalId());
        }
    }
}
