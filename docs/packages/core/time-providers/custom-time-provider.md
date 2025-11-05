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

**Resposta:** Não de forma significativa. Veja os dados reais abaixo.

### 📈 Resultados do Benchmark

Ambiente de teste:
- **Hardware:** AMD Ryzen 5 5600X
- **SO:** Windows 11
- **.NET:** 10.0.0 (RC2)
- **Modo:** Release com otimizações

#### Resultados em Nanosegundos

| Método | Iteração 1 | Iteração 5 | Ratio | Alocação |
|--------|-----------|-----------|-------|----------|
| DateTimeOffset.UtcNow | 24.82 ns | 124.00 ns | 1.00 | - |
| CustomTimeProvider (sem Func) | 24.83 ns | 123.73 ns | 1.00 | - |
| CustomTimeProvider (Func fixo) | 24.89 ns | 123.81 ns | 1.00 | - |
| CustomTimeProvider (Func dinâmico) | 24.87 ns | 123.86 ns | 1.00 | - |

#### 📊 Análise dos Resultados

```
╔══════════════════════════════════════════════════════════════════╗
║                    ANÁLISE DE PERFORMANCE                        ║
╠══════════════════════════════════════════════════════════════════╣
║                                                                  ║
║ ✅ Diferença de Performance: ~0% (praticamente idêntico)        ║
║                                                                  ║
║ ✅ Sem Alocação de Memória: Nenhuma alocação adicional          ║
║                                                                  ║
║ ✅ Escala Consistente: Mantém performance com múltiplas chamadas║
║                                                                  ║
║ ✅ Modo Dinâmico: Tão rápido quanto o nativo                    ║
║                                                                  ║
╚══════════════════════════════════════════════════════════════════╝
```

### 💡 Conclusões Práticas

| Métrica | Resultado | Impacto |
|---------|-----------|--------|
| **Tempo de Execução** | ~25 ns por chamada | ⚡ Imperceptível |
| **Memória** | Zero alocações | ✨ Excelente |
| **Escalabilidade** | Linear | ✅ Previsível |
| **Overhead vs Nativo** | < 1% | 🎯 Negligenciável |

### 🔍 Interpretação dos Números

```
Cenário: Chamar getTime() 1 bilhão de vezes

DateTimeOffset.UtcNow:          24.82 ns × 1B = ~24.82 segundos
CustomTimeProvider:              24.87 ns × 1B = ~24.87 segundos
                                 ─────────────────────────────────
Diferença:                        0.05 segundos em 1 BILHÃO de chamadas

Em termos práticos: 
Economizaria 50 ms em 1B chamadas = imperceptível na aplicação real
```

---

## ⚖️ Trade-offs

| Aspecto | Benefício | Custo |
|--------|-----------|-------|
| **Testabilidade** | ⭐⭐⭐⭐⭐ Excelente | Abstração adicional |
| **Performance** | ✅ Sem impacto | - |
| **Flexibilidade** | ⭐⭐⭐⭐⭐ Máxima | Complexidade mínima |
| **Manutenibilidade** | ✅ Melhor | Requer DI |
| **Simplicidade** | ⚖️ Moderada | Interface clara |

**Conclusão:** Os benefícios superam os custos em praticamente qualquer cenário.

---

## 💡 Exemplos Avançados

### Exemplo 1: Simulação de Passage of Time

```csharp
[Test]
public void SimulateTimeProgression()
{
    var baseTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    int hoursPassed = 0;
    
    Func<TimeZoneInfo?, DateTimeOffset> dynamicTime = _ => 
        baseTime.AddHours(hoursPassed);
    
    var timeProvider = new CustomTimeProvider(
        utcNowFunc: dynamicTime,
        localTimeZone: null
    );
    
    // Hora 0
    Assert.AreEqual(baseTime, timeProvider.GetUtcNow());
    
    // Simular passage de tempo
    hoursPassed = 24;
    var tomorrow = timeProvider.GetUtcNow();
    Assert.AreEqual(baseTime.AddHours(24), tomorrow);
}
```

---

### Exemplo 2: Timezone Múltiplo

```csharp
[Test]
public void TestDifferentTimezones()
{
    var utcTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
    
    var tokyoTz = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
    var timeProviderTokyo = new CustomTimeProvider(
        utcNowFunc: _ => utcTime,
        localTimeZone: tokyoTz
    );
    
    var localTime = timeProviderTokyo.GetLocalNow();
    // UTC 12:00 = Tokyo 21:00 (next day)
    Assert.AreEqual(13, localTime.Day);  // Já é dia 2 em Tóquio
}
```

---

### Exemplo 3: Mock em Testes Complexos

```csharp
public class SchedulerServiceTests
{
    [Test]
    public void ShouldScheduleTasksCorrectly()
    {
        var scheduledTasks = new List<ScheduledTask>();
        var referenceTime = new DateTimeOffset(2024, 1, 1, 8, 0, 0, TimeSpan.Zero);
        
        var timeProvider = new CustomTimeProvider(
