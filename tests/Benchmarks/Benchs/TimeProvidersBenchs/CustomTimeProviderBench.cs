namespace Benchmarks.Benchs.TimeProvidersBenchs;

/// <summary>
/// Benchmark para comparar diferentes abordagens de obter a hora atual com CustomTimeProvider.
/// 
/// Princípios aplicados:
/// - Baseline nativa (DateTimeOffset.UtcNow) para comparação
/// - GlobalSetup para reutilizar instâncias (evita alocações desnecessárias)
/// - Múltiplas iterações realistas (1, 5, 100, 1000)
/// - MemoryDiagnoser para rastrear alocações
/// - Categorização clara entre testes
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1, warmupCount: 3)]
public class CustomTimeProviderBench
    : BenchmarkBase
{
    /*
     * A data fixa precisa ser static para evitar que o valor mude entre as iterações do benchmark.
     * Declarar a variável no escopo do método e passá-la para a função causaria a criação de uma closure
     * gerando alocação de memória desnecessária e impactando os resultados do benchmark.
     */
    private static readonly DateTimeOffset _dateTimeOffsetFixed = DateTimeOffset.UtcNow;

    private PragmaStack.Core.TimeProviders.CustomTimeProvider _customTimeProviderWithoutFunc = null!;
    private PragmaStack.Core.TimeProviders.CustomTimeProvider _customTimeProviderWithFixedFunc = null!;
    private PragmaStack.Core.TimeProviders.CustomTimeProvider _customTimeProviderWithDynamicFunc = null!;

    private Func<TimeZoneInfo?, DateTimeOffset> _fixedValueFunc = null!;
    private Func<TimeZoneInfo?, DateTimeOffset> _dynamicValueFunc = null!;

    [Params(1, 5, 100, 1000)]
    public int IterationCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _customTimeProviderWithoutFunc = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: null, localTimeZone: null);
        
        _fixedValueFunc = (tz) => _dateTimeOffsetFixed;
        _customTimeProviderWithFixedFunc = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: _fixedValueFunc, localTimeZone: null);
        
        _dynamicValueFunc = (tz) => DateTimeOffset.UtcNow;
        _customTimeProviderWithDynamicFunc = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: _dynamicValueFunc, localTimeZone: null);
    }

    [Benchmark(Baseline = true, Description = "DateTimeOffset.UtcNow (Baseline Nativa)")]
    public DateTimeOffset GetUtcNow_DateTimeOffSet_UtcNow()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = DateTimeOffset.UtcNow;
        }

        return lastResult;
    }

    [Benchmark(Description = "CustomTimeProvider com instância padrão")]
    public DateTimeOffset GetUtcNow_CustomTimeProvider_DefaultInstance()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        var timeProvider = PragmaStack.Core.TimeProviders.CustomTimeProvider.DefaultInstance;

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = timeProvider.GetUtcNow();
        }

        return lastResult;
    }

    [Benchmark(Description = "CustomTimeProvider sem Func")]
    public DateTimeOffset GetUtcNow_CustomTimeProvider_WithoutFunc()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = _customTimeProviderWithoutFunc.GetUtcNow();
        }

        return lastResult;
    }

    [Benchmark(Description = "CustomTimeProvider com Func de valor fixo")]
    public DateTimeOffset GetUtcNow_CustomTimeProvider_WithFixedFunc()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = _customTimeProviderWithFixedFunc.GetUtcNow();
        }

        return lastResult;
    }

    [Benchmark(Description = "CustomTimeProvider com Func de valor dinâmico")]
    public DateTimeOffset GetUtcNow_CustomTimeProvider_WithDynamicFunc()
    {
        DateTimeOffset lastResult = DateTimeOffset.MinValue;

        for (int i = 0; i < IterationCount; i++)
        {
            lastResult = _customTimeProviderWithDynamicFunc.GetUtcNow();
        }

        return lastResult;
    }
}
