# ⏰ Custom Time Provider

A classe `CustomTimeProvider` permite que você defina uma fonte personalizada de tempo para sua aplicação. Isso é útil em cenários onde você deseja controlar o tempo de forma precisa, como em testes de unidade ou simulações.

> 💡 **Visão Geral:** Implemente uma abstração de tempo testável, permitindo controle total sobre data/hora em diferentes ambientes.

---

## 📚 Sumário

- [Contexto: Por Que Existe](#-contexto-por-que-existe)
- [Problemas Resolvidos](#-problemas-resolvidos)
- [Funcionalidades](#-funcionalidades)
- [Como Usar](#-como-usar)
- [Impacto na Performance](#-impacto-na-performance)
- [Trade-offs](#-tradeoffs)
- [Exemplos Avançados](#-exemplos-avançados)
- [Referências](#-referências)

---

## 📍 Contexto: Por Que Existe

### O Problema Real

Em muitas aplicações, o tempo é obtido diretamente do sistema operacional via `DateTime.UtcNow`, o que **dificulta testes e simulações**. 

**Exemplo de desafio comum:**

Você precisa testar uma funcionalidade que **expira cadastros não atualizados há 7 dias**.

```csharp
❌ Abordagem problemática:
1. Criar um cadastro
2. Esperar 7 dias reais ⏳
3. Verificar se foi expirado

❌ Problemas:
- Testes levariam 7 dias para passar
- Resultados inconsistentes
- Impossível testar em CI/CD
- Não é prático para testes automatizados
```

### A Solução

A partir do **.NET 6+**, podemos criar um `TimeProvider` personalizado para **abstrair a fonte de tempo**.

```csharp
✅ Abordagem com CustomTimeProvider:
1. Criar um cadastro
2. Injetar um TimeProvider com hora customizada (+7 dias)
3. Verificar se foi expirado

✅ Benefícios:
- Testes executam em milissegundos
- Resultados consistentes e reproduzíveis
- Funciona perfeitamente em CI/CD
- Ideal para testes automatizados
```

---

## 🔴 Problemas Resolvidos

### 1. ⏱️ Dependência de Tempo Real

**Problema:** Código acoplado ao relógio do sistema

```csharp
❌ Código sem injeção de dependência:
public class RegistrationService
{
    public void ExpireOldRegistrations()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-7);  // ← Acoplado ao relógio do sistema
        var oldRegistrations = _registrationRepository.GetRegistrationsBefore(cutoffDate);
        foreach (var registration in oldRegistrations)
        {
            registration.Expire();
        }
    }
}

Impacto nos testes: 🔴 IMPOSSÍVEL testar sem esperar 7 dias
```

**Solução:** Injetar o TimeProvider

```csharp
✅ Código com injeção de dependência:
public class RegistrationService
{
    private readonly TimeProvider _timeProvider;

    public RegistrationService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void ExpireOldRegistrations()
    {
        var cutoffDate = _timeProvider.GetUtcNow().AddDays(-7);  // ← Controlável
        var oldRegistrations = _registrationRepository.GetRegistrationsBefore(cutoffDate);
        foreach (var registration in oldRegistrations)
        {
            registration.Expire();
        }
    }
}

Impacto nos testes: ✅ TESTÁVEL com tempo customizado
```

---

### 2. 🧪 Testes Inconsistentes

**Problema:** Diferentes resultados em diferentes horas do dia

```
Teste rodado às 9:00 → Resultado A
Teste rodado às 14:00 → Resultado B  (diferentes!)
```

**Solução:** Tempo fixo no teste

```csharp
[Test]
public void ShouldExpireOldRegistrations()
{
    var fixedTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
    var timeProvider = new CustomTimeProvider(
        utcNowFunc: _ => fixedTime,
        localTimeZone: null
    );
    
    var service = new RegistrationService(timeProvider);
    service.ExpireOldRegistrations();
    
    // Resultado SEMPRE o mesmo, independente de quando o teste roda ✅
}
```

---

### 3. 🚀 Performance em Testes

**Problema:** Esperar tempo real torna testes lentos

```
Antes (sem CustomTimeProvider):
- Criar registros
- Esperar 7 dias reais
- Verificar expiração
Tempo total: 7 dias ⏳😞

Depois (com CustomTimeProvider):
- Criar registros
- Usar tempo customizado
- Verificar expiração
Tempo total: 1ms ⚡😊
```

---

## 💚 Funcionalidades

### ✅ Modo Sistema (Produção)
Retorna a data/hora atual do sistema operacional, funcionando como um `TimeProvider` normal.

```csharp
var timeProvider = new CustomTimeProvider(utcNowFunc: null, localTimeZone: null);
var now = timeProvider.GetUtcNow();  // Hora do sistema
```

### ✅ Modo Fixo (Testes)
Retorna uma data/hora fixa configurada, ideal para testes determinísticos.

```csharp
var fixedTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
var timeProvider = new CustomTimeProvider(
    utcNowFunc: _ => fixedTime,
    localTimeZone: null
);
var now = timeProvider.GetUtcNow();  // Sempre 2024-01-01 12:00:00
```

### ✅ Timezone Customizado
Permite especificar um timezone para operações de horário local.

```csharp
var saoPauloTz = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
var timeProvider = new CustomTimeProvider(
    utcNowFunc: null,
    localTimeZone: saoPauloTz
);
var localTime = timeProvider.GetLocalNow();  // Horário de São Paulo
```

### ✅ Instância Padrão
Acessível via `CustomTimeProvider.Default` para uso sem injeção de dependência.

```csharp
var now = CustomTimeProvider.Default.GetUtcNow();  // Uso direto
```

---

## 🚀 Como Usar

### 1️⃣ Uso Sem Injeção de Dependência - Hora do Sistema

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var timeProvider = PragmaStack.Core.TimeProviders.CustomTimeProvider.DefaultInstance;
        
        Console.WriteLine($"Hora UTC Atual: {timeProvider.GetUtcNow()}");
        Console.WriteLine($"Hora Local Atual: {timeProvider.GetLocalNow()}");
    }
}
```

**Quando usar:** Prototipos rápidos, scripts, ou quando não precisa de testes automatizados.

---

### 2️⃣ Uso Sem Injeção de Dependência - Hora Customizada

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var expectedTime = new DateTimeOffset(
            year: 2024,
            month: 1,
            day: 1,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero
        );
        
        Func<TimeZoneInfo?, DateTimeOffset> customUtcNowFunc = (tz) => expectedTime;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: customUtcNowFunc,
            localTimeZone: null
        );

        Console.WriteLine($"Hora UTC Customizada: {timeProvider.GetUtcNow()}");
        Console.WriteLine($"Hora Local Customizada: {timeProvider.GetLocalNow()}");
    }
}
```

**Quando usar:** Simulações, testes manuais, ou demonstrações.

---

### 3️⃣ Uso Com Injeção de Dependência - Hora do Sistema (Recomendado para Produção)

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        
        // Registrar TimeProvider no container
        services.AddSingleton<TimeProvider>(
            _ => new PragmaStack.Core.TimeProviders.CustomTimeProvider(
                utcNowFunc: null,
                localTimeZone: null
            )
        );
        
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<SomeService>();
        
        service.ProcessTime();
    }
}

public class SomeService
{
    private readonly TimeProvider _timeProvider;

    public SomeService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void ProcessTime()
    {
        Console.WriteLine($"Hora UTC: {_timeProvider.GetUtcNow()}");
        Console.WriteLine($"Hora Local: {_timeProvider.GetLocalNow()}");
    }
}
```

**Quando usar:** Aplicações de produção com injeção de dependência.

---

### 4️⃣ Uso Com Injeção de Dependência - Hora Customizada (Recomendado para Testes)

```csharp
public class ExpirationServiceTests
{
    [Test]
    public void ShouldExpireRegistrationsOlderThan7Days()
    {
        // Arrange - Configurar tempo fixo
        var referenceTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Func<TimeZoneInfo?, DateTimeOffset> fixedTimeFunc = _ => referenceTime;
        
        var timeProvider = new CustomTimeProvider(
            utcNowFunc: fixedTimeFunc,
            localTimeZone: null
        );
        
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var service = serviceProvider.GetRequiredService<RegistrationService>();
        service.ExpireOldRegistrations();
        
        // Assert
        var expiredRegistrations = GetExpiredRegistrations();
        Assert.AreEqual(expectedCount, expiredRegistrations.Count);
    }
}
```

**Quando usar:** Testes unitários com tempo controlado (RECOMENDADO ⭐).

---

## 📊 Impacto na Performance

### 🎯 A Grande Pergunta

> "Será que o uso do TimeProvider impacta a performance da minha aplicação?"

**Resposta:** Depende da implementação, mas a maioria dos cenários tem impacto mínimo ou nenhum. Veja os dados reais abaixo.

### 📈 Resultados do Benchmark

Ambiente de teste:
- **Hardware:** AMD Ryzen 5 5600X (3.70GHz, 12 cores)
- **SO:** Windows 11 (10.0.26200.7019)
- **.NET:** 10.0.0 (RC2)
- **Modo:** Release com otimizações (x86-64-v3)
- **Warm-up:** 3 iterações antes das medições

#### 📊 Tabela de Resultados Completa

| Método | Iterações | Mean | Error | Ratio | Análise |
|--------|-----------|------|-------|-------|---------|
| **DateTimeOffset.UtcNow (Baseline)** | 1 | 24.87 ns | 0.025 ns | 1.00 | Referência |
| CustomTimeProvider com instância padrão | 1 | 25.38 ns | 0.112 ns | **1.02** ✅ | ~2% mais lento |
| CustomTimeProvider sem Func | 1 | 29.13 ns | 0.079 ns | **1.17** ✅ | ~17% mais lento |
| **CustomTimeProvider com Func fixo** | 1 | **1.41 ns** | 0.048 ns | **0.06** 🚀 | **94% mais rápido!** |
| CustomTimeProvider com Func dinâmico | 1 | 29.13 ns | 0.070 ns | **1.17** ✅ | ~17% mais lento |
| | | | | | |
| **DateTimeOffset.UtcNow (Baseline)** | 5 | 124.16 ns | 0.205 ns | 1.00 | Referência |
| CustomTimeProvider com instância padrão | 5 | 124.84 ns | 0.133 ns | **1.01** ✅ | ~1% mais lento |
| CustomTimeProvider sem Func | 5 | 128.89 ns | 0.215 ns | **1.04** ✅ | ~4% mais lento |
| **CustomTimeProvider com Func fixo** | 5 | **6.78 ns** | 0.082 ns | **0.05** 🚀 | **95% mais rápido!** |
| CustomTimeProvider com Func dinâmico | 5 | 129.18 ns | 0.205 ns | **1.04** ✅ | ~4% mais lento |
| | | | | | |
| **DateTimeOffset.UtcNow (Baseline)** | 100 | 2,498.04 ns | 3.31 ns | 1.00 | Referência |
| CustomTimeProvider com instância padrão | 100 | 2,503.56 ns | 3.04 ns | **1.00** ✅ | ~0% diferença |
| CustomTimeProvider sem Func | 100 | 2,526.14 ns | 3.07 ns | **1.01** ✅ | ~1% mais lento |
| **CustomTimeProvider com Func fixo** | 100 | **71.71 ns** | 0.334 ns | **0.03** 🚀 | **97% mais rápido!** |
| CustomTimeProvider com Func dinâmico | 100 | 2,526.29 ns | 4.08 ns | **1.01** ✅ | ~1% mais lento |
| | | | | | |
| **DateTimeOffset.UtcNow (Baseline)** | 1000 | 25,006.67 ns | 38.98 ns | 1.00 | Referência |
| CustomTimeProvider com instância padrão | 1000 | 24,881.02 ns | 30.38 ns | **0.99** ✅ | ~1% mais rápido |
| CustomTimeProvider sem Func | 1000 | 24,931.88 ns | 31.39 ns | **1.00** ✅ | ~0% diferença |
| **CustomTimeProvider com Func fixo** | 1000 | **672.37 ns** | 3.64 ns | **0.03** 🚀 | **97% mais rápido!** |
| CustomTimeProvider com Func dinâmico | 1000 | 25,314.50 ns | 200.79 ns | **1.01** ✅ | ~1% mais lento |

---

#### 🔍 Análise Detalhada por Cenário

### 📍 Cenário 1: Chamada Única (1 iteração)

```
╔═══════════════════════════════════════════════════════════════════╗
║           PERFORMANCE: OPERAÇÃO INDIVIDUAL                        ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║ Baseline (DateTimeOffset.UtcNow):     24.87 ns                   ║
║                                                                   ║
║ ✅ CustomTimeProvider (instância padrão):  25.38 ns (+1.02x)     ║
║    → Praticamente idêntico, impacto imperceptível                ║
║                                                                   ║
║ ✅ CustomTimeProvider (sem Func):         29.13 ns (+1.17x)      ║
║    → Mínimo overhead de abstração                                ║
║                                                                   ║
║ 🚀 CustomTimeProvider (Func fixo):        1.41 ns (0.06x) ⭐     ║
║    → MUITO mais rápido! (compilador otimiza)                     ║
║                                                                   ║
║ ✅ CustomTimeProvider (Func dinâmico):    29.13 ns (+1.17x)      ║
║    → Impacto é da lógica, não da abstração                       ║
║                                                                   ║
╚═══════════════════════════════════════════════════════════════════╝
```

**Conclusão:** Em operações individuais, o CustomTimeProvider tem impacto mínimo.

---

### 📍 Cenário 2: Pequeno Batch (5 iterações)

```
╔═══════════════════════════════════════════════════════════════════╗
║           PERFORMANCE: PEQUENO BATCH (5x)                         ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║ Baseline (DateTimeOffset.UtcNow):     124.16 ns                  ║
║                                                                   ║
║ ✅ CustomTimeProvider (instância padrão):  124.84 ns (+1.01x)    ║
║    → Indistinguível na prática                                   ║
║                                                                   ║
║ ✅ CustomTimeProvider (sem Func):         128.89 ns (+1.04x)     ║
║    → Diferença de apenas 4 ns por iteração                       ║
║                                                                   ║
║ 🚀 CustomTimeProvider (Func fixo):        6.78 ns (0.05x) ⭐     ║
║    → Incrível otimização do compilador                           ║
║                                                                   ║
║ ✅ CustomTimeProvider (Func dinâmico):    129.18 ns (+1.04x)     ║
║    → Comporta-se como esperado                                   ║
║                                                                   ║
╚═══════════════════════════════════════════════════════════════════╝
```

**Conclusão:** Em batches pequenos, performance praticamente idêntica.

---

### 📍 Cenário 3: Batch Normal (100 iterações)

```
╔═══════════════════════════════════════════════════════════════════╗
║           PERFORMANCE: BATCH NORMAL (100x)                        ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║ Baseline (DateTimeOffset.UtcNow):     2,498.04 ns (2.5 µs)       ║
║                                                                   ║
║ ✅ CustomTimeProvider (instância padrão):  2,503.56 ns (+1.00x)  ║
║    → Virtualmente idêntico                                       ║
║                                                                   ║
║ ✅ CustomTimeProvider (sem Func):         2,526.14 ns (+1.01x)   ║
║    → Diferença: apenas 28 ns em 100 chamadas (0.28 ns/chamada) ║
║                                                                   ║
║ 🚀 CustomTimeProvider (Func fixo):        71.71 ns (0.03x) ⭐    ║
║    → ~97% mais rápido! (compilador remove muita coisa)          ║
║                                                                   ║
║ ✅ CustomTimeProvider (Func dinâmico):    2,526.29 ns (+1.01x)   ║
║    → Comportamento consistente e previsível                      ║
║                                                                   ║
╚═══════════════════════════════════════════════════════════════════╝
```

**Conclusão:** Escala perfeitamente, mantendo proporção consistente.

---

### 📍 Cenário 4: Carga Alta (1000 iterações)

```
╔═══════════════════════════════════════════════════════════════════╗
║           PERFORMANCE: CARGA ALTA (1000x)                         ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║ Baseline (DateTimeOffset.UtcNow):     25,006.67 ns (25.0 µs)     ║
║                                                                   ║
║ ✅ CustomTimeProvider (instância padrão):  24,881.02 ns (-0.99x) ║
║    → MAIS RÁPIDO que o baseline!                                 ║
║    (variabilidade normal em benchmarks)                          ║
║                                                                   ║
║ ✅ CustomTimeProvider (sem Func):         24,931.88 ns (+1.00x)  ║
║    → Praticamente idêntico (0 diferença prática)                 ║
║                                                                   ║
║ 🚀 CustomTimeProvider (Func fixo):        672.37 ns (0.03x) ⭐   ║
║    → ~97% mais rápido! (otimizações agressivas)                 ║
║                                                                   ║
║ ✅ CustomTimeProvider (Func dinâmico):    25,314.50 ns (+1.01x)  ║
║    → Mantém escalabilidade linear                                ║
║                                                                   ║
╚═══════════════════════════════════════════════════════════════════╝
```

**Conclusão:** Comportamento escalável e previsível em alta carga.

---

### 💡 Interpretação Prática dos Números

#### Cenário Real: Aplicação Web com 1000 requisições/segundo

```
Se cada requisição chama GetUtcNow() 100 vezes:

Baseline (DateTimeOffset.UtcNow):
  100 chamadas × 2,498.04 ns = 249,804 ns = 0.25 ms por requisição
  1000 requisições/s × 0.25 ms = 250 ms de overhead

CustomTimeProvider (sem Func):
  100 chamadas × 2,526.14 ns = 252,614 ns = 0.25 ms por requisição
  1000 requisições/s × 0.25 ms = 252 ms de overhead
  
Diferença: 2 ms em 1000 requisições = 0.002 ms por requisição = IMPERCEPTÍVEL ✅

CustomTimeProvider (Func fixo - testes):
  100 chamadas × 71.71 ns = 7,171 ns = 0.007 ms por requisição
  1000 requisições/s × 0.007 ms = 7 ms overhead
  
MELHORIA: 243 ms economizados! (testes rodam muito mais rápido) 🚀
```

---

### 📊 Análise dos Resultados

```
╔═══════════════════════════════════════════════════════════════════════════╗
║                        CONCLUSÕES PRINCIPAIS                             ║
╠═══════════════════════════════════════════════════════════════════════════╣
║                                                                           ║
║ 1️⃣ PRODUÇÃO (Func dinâmico):                                             ║
║    └─ Impacto percentual: +1-17% em operações isoladas                    ║
║    └─ Impacto absoluto: +4 a +4 ns por chamada (nanosegundos!)            ║
║       → CustomTimeProvider: 29.13 ns vs. Baseline: 24.87 ns               ║
║       → Diferença: 4.26 ns por chamada                                    ║
║    └─ Em batches (>5 iterações): praticamente 0% de diferença            ║
║       → CustomTimeProvider: 129.18 ns (5x) vs. Baseline: 124.16 ns       ║
║       → Diferença: 0.8 ns por chamada individual                          ║
║    └─ Escalabilidade: mantém proporção consistente                       ║
║    └─ Alocação: ZERO bytes adicionais                                    ║
║    └─ Impacto REAL em produção: IMPERCEPTÍVEL ✅                         ║
║    └─ CONTEXTO: 4 nanosegundos = 0.000004 milissegundos                  ║
║       → Você faria 250.000.000 chamadas para perder 1 segundo             ║
║    ✅ RECOMENDADO: Sim! Benefícios superam os custos insignificantes      ║
║                                                                           ║
║ 2️⃣ TESTES (Func fixo):                                                   ║
║    └─ Impacto percentual: -94% a -97% (MUITO mais rápido!)                ║
║    └─ Impacto absoluto: -23.46 ns por chamada (GANHO!)                    ║
║       → CustomTimeProvider Func fixo: 1.41 ns vs. Baseline: 24.87 ns      ║
║       → Diferença: economiza 23.46 ns por chamada                         ║
║    └─ Em batches (100x): economiza ~2,426 ns por batch                   ║
║       → CustomTimeProvider: 71.71 ns vs. Baseline: 2,498.04 ns            ║
║       → Diferença: 2,426.33 ns economizados por batch                     ║
║    └─ Razão: Compilador otimiza funções constantes                       ║
║    └─ Resultado: Testes rodam SIGNIFICATIVAMENTE mais rápido             ║
║    └─ Alocação: ZERO bytes adicionais                                    ║
║    └─ Impacto REAL em testes: MUITO POSITIVO 🚀                          ║
║    └─ CONTEXTO: Economiza ~2.5 microsegundos por batch                   ║
║       → Em 10.000 batchs no suite de testes = 25 milissegundos ganhos     ║
║    🚀 RECOMENDADO: DEFINITIVAMENTE! Ganho REAL de performance             ║
║                                                                           ║
║ 3️⃣ INSTÂNCIA PADRÃO:                                                     ║
║    └─ Impacto percentual: +1-2% em operações isoladas                     ║
║    └─ Impacto absoluto: +0.51 ns por chamada                              ║
║       → CustomTimeProvider Default: 25.38 ns vs. Baseline: 24.87 ns       ║
║       → Diferença: 0.51 ns por chamada                                    ║
║    └─ Em batches: praticamente idêntico (+0.68 ns em 5x)                 ║
║    └─ Acesso: simples via CustomTimeProvider.Default                     ║
║    └─ Impacto REAL: NEGLIGENCIÁVEL ✅                                    ║
║    └─ CONTEXTO: 0.51 nanosegundos por chamada                            ║
║       → Você faria ~2 BILHÕES de chamadas para perder 1 segundo          ║
║    ✅ RECOMENDADO: Para prototipos e scripts rápidos                     ║
║                                                                           ║
╚═══════════════════════════════════════════════════════════════════════════╝
