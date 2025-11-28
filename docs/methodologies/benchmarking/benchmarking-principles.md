# 📊 Princípios de Benchmarking

Não sou especialista em benchmarking e nem tenho a pretenção de medir o desempenho de forma exata. Porém, ao longo do tempo, desenvolvi algumas práticas que me ajudam a obter resultados mais confiáveis e úteis.

> 💡 **Filosofia:** Busco resultados suficientemente bons para comparar diferentes abordagens e identificar melhorias de desempenho, não medições cientificamente perfeitas.

---

## 📚 Sumário

- [Realidade vs. Teoria](#-realidade-vs-teoria)
- [Princípios Fundamentais](#-princípios-fundamentais)
- [Objetivo](#-objetivo)
- [Como Interpretar Resultados](#-como-interpretar-os-resultados)
- [Exemplos Práticos](#-exemplos-práticos)
- [Referências](#-referências)

---

## 🎯 Realidade vs. Teoria

### O Ideal
Para ter um benchmark preciso é importante ter um ambiente controlado, onde as variáveis que podem afetar o desempenho sejam minimizadas:
- ✅ Hardware consistente
- ✅ Sem processos em segundo plano
- ✅ Sistema em estado estável
- ✅ Sem variações de rede ou I/O

### A Realidade
Não temos esse ambiente disponível em nosso dia a dia. **O foco aqui é em práticas que podem ser aplicadas em qualquer cenário** para obter resultados confiáveis e úteis.

**Resultado:** Os dados não serão perfeitos, mas serão suficientemente bons para análise.

---

## 🏆 Princípios Fundamentais

### 1. ⚙️ Modo Release Obrigatório

```
❌ Debug       → Sem otimizações, overhead de debug, resultados irrealistas
✅ Release     → Otimizações do compilador, reflete performance real
```

**Por quê:** O compilador aplica otimizações agressivas no modo Release que não existem no Debug. Seus resultados devem refletir o comportamento em produção.

---

### 2. 📌 Baseline Nativa para Comparação

**Regra:** Quando testando uma implementação customizada, a baseline deve ser o comportamento nativo do .NET.

**Exemplos:**

| O que está testando | Baseline deve ser |
|-------------------|------------------|
| Dictionary customizado | `Dictionary<TKey, TValue>` |
| List customizado | `List<T>` |
| TimeProvider personalizado | `DateTime.UtcNow` ou `TimeProvider` nativo |

**Benefício:** Garante comparação com o melhor que o framework oferece nativamente.

---

### 3. 🔄 Múltiplas Execuções

**Recomendação:** Execute o benchmark várias vezes (mínimo 3-5 vezes).

**Por quê:**
- Reduz variabilidade aleatória
- Permite calcular média, desvio padrão e intervalo de confiança
- Identifica outliers (execuções anormais)
- Aumenta confiabilidade dos resultados

```
1 execução   → 24.88 ns  ❌ Pode ser sorte ou azar
5 execuções  → Média: 24.90 ns ✅ Mais confiável
```

---

### 4. 🧹 Evitar Otimizações do Compilador

**Problema:** O compilador pode remover código "inútil" que você está tentando medir.

**Solução:** Garanta que o resultado seja utilizado de alguma forma.

**❌ Código Ruim (pode ser otimizado):**
```csharp
[Benchmark]
public void GetTime_Bad()
{
    var now = timeProvider.GetUtcNow();
    // Resultado não é usado - compilador pode remover!
}
```

**✅ Código Correto:**
```csharp
[Benchmark]
public DateTimeOffset GetTime_Good()
{
    var now = timeProvider.GetUtcNow();
    return now;  // Resultado é retornado e usado
}
```

---

### 5. 📈 Iterações Realistas

**Abordagem em Camadas:**

```
1. Uma Iteração (baseline)
   └─ Mede custo individual da operação

2. Múltiplas Iterações (realista)
   └─ Simula uso real (ex: 5 chamadas durante processamento)

3. Carga Alta (stress test)
   └─ Como escala com milhares de operações?
```

**Exemplo:**
```csharp
[Benchmark]
[ArgumentsSource(nameof(IterationCounts))]
public DateTimeOffset GetTime(int iterations)
{
    DateTimeOffset result = default;
    for (int i = 0; i < iterations; i++)
    {
        result = timeProvider.GetUtcNow();
    }
    return result;
}

public IEnumerable<int> IterationCounts()
{
    yield return 1;      // Operação única
    yield return 5;      // Pequeno batch
    yield return 100;    // Batch normal
    yield return 1000;   // Carga alta
}
```

---

### 6. 🎯 Escopo Claro e Focado

**Regra:** Cada benchmark deve testar **uma coisa e apenas uma coisa**.

**❌ Escopo Ruim:**
```csharp
[Benchmark]
public void ProcessData_TooBroad()
{
    ValidateInput();      // Testa validação
    ProcessCore();        // Testa processamento
    SerializeResult();    // Testa serialização
    SaveToDatabase();     // Testa I/O
    // Qual parte é lenta? Não sabemos!
}
```

**✅ Escopo Bom:**
```csharp
[Benchmark]
public Data ProcessCore()
{
    // Testa APENAS o processamento central
    return new Data { Value = input * 2 };
}
```

**Benefício:** Resultados específicos e relevantes.

---

## 🎯 Objetivo

O objetivo principal do benchmark é:

> **Comparar diferentes implementações ou abordagens para uma mesma funcionalidade, identificando qual oferece o melhor desempenho em termos de tempo de execução e uso de recursos.**

### Equilíbrio Essencial

```
╔════════════════════════════════════════╗
║  Rigor Científico ←→ Praticidade       ║
║                                        ║
║  Buscamos resultados que sejam:        ║
║  • Úteis e aplicáveis                  ║
║  • Reproduzíveis razoavelmente        ║
║  • Obtidos com tempo razoável         ║
╚════════════════════════════════════════╝
```

---

## 📊 Como Interpretar os Resultados

### Exemplo de Resultado Real

| Method | IterationCount | Mean | Error | StdDev | Ratio | Allocated |
|--------|---|---|---|---|---|---|
| DateTimeOffSet.UtcNow | 1 | 24.88 ns | 0.047 ns | 0.044 ns | 1.00 | - |
| CustomTimeProvider (sem Func) | 1 | 24.91 ns | 0.037 ns | 0.032 ns | 1.00 | - |
| CustomTimeProvider (Func fixo) | 1 | 24.91 ns | 0.016 ns | 0.015 ns | 1.00 | - |
| CustomTimeProvider (Func dinâmico) | 1 | 24.91 ns | 0.031 ns | 0.027 ns | 1.00 | - |
| | | | | | | |
| DateTimeOffSet.UtcNow | 5 | 124.21 ns | 0.367 ns | 0.343 ns | 1.00 | - |
| CustomTimeProvider (sem Func) | 5 | 123.99 ns | 0.138 ns | 0.129 ns | 1.00 | - |
| CustomTimeProvider (Func fixo) | 5 | 123.71 ns | 0.119 ns | 0.111 ns | 1.00 | - |
| CustomTimeProvider (Func dinâmico) | 5 | 123.91 ns | 0.121 ns | 0.113 ns | 1.00 | - |

### 📋 Legenda de Benchmarking - Tabela de Referência

| Coluna | O Que É | Como Interpretar |
|--------|--------|------------------|
| **IterationCount** | Valor do parâmetro de iterações | Quantas vezes a operação foi executada por medição |
| **Mean** | Média aritmética de todas as medições | Principal indicador de velocidade. Compare entre métodos |
| **Error** | Metade do intervalo de confiança de 99,9% | Quanto a média pode variar. Menor = mais confiável |
| **StdDev** | Desvio padrão das medições | Variabilidade dos resultados. Menor = mais consistente |
| **Ratio** | Comparação com baseline ([Atual]/[Baseline]) | <1 = mais rápido; >1 = mais lento; 1.0 = igual |
| **Completed Work Items** | Itens processados no ThreadPool | Indica paralelismo; `-` = execução sequencial |
| **Lock Contentions** | Contenção de locks de Monitor | Quanto mais alto, mais contenção entre threads |
| **Allocated** | Memória alocada por operação | Em bytes. Memória gerenciada apenas |
| **Alloc Ratio** | Razão de alocação vs. baseline | <1 = menos memória; >1 = mais memória |
| **1 ns** | 1 Nanossegundo | 0.000000001 segundo; 1000 ns = 1 microsegundo |

---

## 🔍 Como Analisar Cada Métrica

### 1. 📈 Média (Mean)

**O quê:** Tempo médio de execução de cada operação.

**Como interpretar:**
```
Método A: 24.88 ns
Método B: 49.76 ns
Razão: B é 2x mais lento que A

Impacto prático: 25 ns por operação
• Se executado 1 vez → imperceptível
• Se executado 1M vezes → 25ms economizados
```

---

### 2. ✅ Erro (Error)

**O quê:** Precisão da medição (intervalo de confiança 99,9%).

**Como interpretar:**
```
Mean: 24.88 ns ± 0.047 ns  (Erro Baixo ✅)
└─ Resultado muito confiável, pouca variação

Mean: 24.88 ns ± 5.000 ns  (Erro Alto ❌)
└─ Resultado menos confiável, muita variação
```

---

### 3. 📊 Desvio Padrão (StdDev)

**O quê:** Quanto os resultados variam entre as medições.

**Como interpretar:**
```
Mean: 100 ns, StdDev: 1 ns   ✅ Consistente (1% de variação)
Mean: 100 ns, StdDev: 50 ns  ❌ Inconsistente (50% de variação)
```

**Causa de alto StdDev:**
- Interferência do SO
- Garbage Collection
- Thermal Throttling
- Contexto insuficiente para warm-up

---

### 4. 📊 Razão (Ratio)

**O quê:** Comparação de desempenho com a baseline (primeira linha).

**Como interpretar:**
```
Ratio: 1.00  → Desempenho idêntico à baseline
Ratio: 0.80  → 20% mais rápido que a baseline
Ratio: 1.25  → 25% mais lento que a baseline
```

**Exemplo prático:**
```
Baseline (DateTime.UtcNow):     1.00 ✓
CustomTimeProvider (sem Func):  1.00 ✓ Nenhuma diferença!
CustomTimeProvider (com Func):  0.99 ✓ Praticamente igual
```

---

### 5. 💾 Alocação de Memória (Allocated)

**O quê:** Bytes alocados em memória gerenciada por operação.

**Como interpretar:**
```
Allocated: -    → Sem alocações (excelente!)
Allocated: 48B  → Uma pequena alocação por operação
Allocated: 4KB  → Alocação significativa

Impacto real:
• 48B alocado por operação × 1M operações = 48MB
• Isto causa pressão no GC e pausas
```

---

### 6. 📊 Alloc Ratio

**O quê:** Comparação de alocação vs. baseline.

**Como interpretar:**
```
Alloc Ratio: 1.0   → Mesma quantidade de memória
Alloc Ratio: 0.5   → Metade da memória alocada
Alloc Ratio: 2.0   → Duas vezes mais memória

Decisão:
"Método A é 10% mais rápido, mas aloca 5x mais"
→ Avaliar trade-off conforme contexto
```

---

## 💡 Exemplo Prático Completo

### Cenário: Comparar duas formas de obter a hora atual

**Benchmark bem escrito:**

```csharp
[SimpleJob(RunStrategy.ColdStart, warmupCount: 3, targetCount: 5)]
[GroupBenchmarkAttribute]
[MemoryDiagnoser]
public class TimeProviderBenchmark
{
    private TimeProvider _timeProvider = null!;
    
    [GlobalSetup]
    public void Setup()
    {
        _timeProvider = new CustomTimeProvider(null, null);
    }

    [BenchmarkCategory("Single")]
    [Benchmark(Baseline = true)]
    public DateTimeOffset DateTime_UtcNow()
    {
        return DateTimeOffset.UtcNow;
    }

    [BenchmarkCategory("Single")]
    [Benchmark]
    public DateTimeOffset CustomTimeProvider_UtcNow()
    {
        return _timeProvider.GetUtcNow();
    }

    [BenchmarkCategory("Multiple")]
    [Arguments(5)]
    [Arguments(100)]
    [Arguments(1000)]
    [Benchmark(Baseline = true)]
    public DateTimeOffset DateTime_UtcNow_Multiple(int count)
    {
        DateTimeOffset result = default;
        for (int i = 0; i < count; i++)
        {
            result = DateTimeOffset.UtcNow;
        }
        return result;
    }

    [BenchmarkCategory("Multiple")]
    [Arguments(5)]
    [Arguments(100)]
    [Arguments(1000)]
    [Benchmark]
    public DateTimeOffset CustomTimeProvider_UtcNow_Multiple(int count)
    {
        DateTimeOffset result = default;
        for (int i = 0; i < count; i++)
        {
            result = _timeProvider.GetUtcNow();
        }
        return result;
    }
}
```

**O que isso faz bem:**
- ✅ Modo Release automático
- ✅ Warm-up adequado (3 iterações)
- ✅ Múltiplas medições (5 execuções)
- ✅ Sem alocações desnecessárias
- ✅ Resultados são utilizados
- ✅ Múltiplos tamanhos testados
- ✅ Diagnóstico de memória ativado
- ✅ Baseline clara

---

## ⚖️ Trade-offs a Considerar

| Aspecto | Prioridade Alta | Prioridade Média | Prioridade Baixa |
|--------|-----------------|------------------|------------------|
| **Tempo para resultado** | Horas | Dias | Semanas |
| **Precisão desejada** | ±10% | ±5% | ±1% |
| **Ambientes diferentes** | Não | Sim | Sim |
| **Documentação** | Básica | Média | Completa |
| **Valor prático** | Imediato | Futuro | Acadêmico |

**Princípio:** Adapte o rigor conforme a importância da decisão.

---

## 🔗 Referências

- 📖 [Armadilhas Comuns em Benchmarking](./benchmarking-pitfalls.md)
- 🔗 [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- 📚 [Documentação PragmaStack](../README.md)

---

## 📝 Checklist Final

Antes de publicar seus resultados:

```
□ Executado em modo Release
□ Baseline nativa definida
□ Mínimo 3-5 execuções
□ Sem compilador removendo código
□ Múltiplas iterações testadas
□ Escopo claro e focado
□ Erro aceitável (< 10% do Mean)
□ StdDev razoável
□ Memória analisada
□ Contexto documentado
□ Conclusões práticas extraídas
```

---

## ⚠️ Disclaimer

> As recomendações neste documento foram desenvolvidas baseadas em experiência prática pessoal. Não as trate como verdade absoluta. **Adapte os princípios conforme o contexto específico do seu projeto** e necessidades de performance.

---

<div align="center">

**Desenvolvido por Marcelo Castelo Branco**


</div>
