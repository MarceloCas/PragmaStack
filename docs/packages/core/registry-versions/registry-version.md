# 📦 RegistryVersion - Versões Monotônicas para Optimistic Locking

A estrutura `RegistryVersion` fornece geração ultrarrápida de números de versão monotônicos baseados em UTC ticks, com garantia de ordenação temporal e proteção contra clock drift. Ideal para optimistic locking, event sourcing e audit logs.

> 💡 **Visão Geral:** Gere versões monotônicas em ~25 nanosegundos, com **garantia de ordenação** por thread e **50% menos espaço** que Guid — perfeito para versionamento de entidades sem overhead.

## 🎯 Por Que Usar RegistryVersion ao Invés de Alternativas?

| Característica | Versão Gerada no Banco | **`RegistryVersion.GenerateNewVersion()`** | `Id.GenerateNewId()` (UUIDv7) |
|----------------|------------------------|---------------------------------------------|-------------------------------|
| **Performance** | ⚠️ ~1-5ms (round-trip) | ✅ **~25ns** (3x mais rápido que Id) | ✅ ~73ns |
| **Tamanho** | 4-8 bytes | ✅ **8 bytes** | 16 bytes |
| **Monotônico por thread?** | N/A | ✅ **SIM** (garantido) | ✅ SIM |
| **Proteção contra clock drift** | N/A | ✅ **SIM** | ✅ SIM |
| **Geração offline/local** | ❌ NÃO (depende do banco) | ✅ **SIM** (~25ns) | ✅ SIM (~73ns) |
| **Único globalmente sem coordenação** | ❌ NÃO | ❌ **NÃO** (requer coordenação) | ✅ SIM (46 bits random) |
| **Ideal para** | N/A | ✅ **Versioning, sequences** | ✅ Primary keys distribuídos |

**RegistryVersion** é perfeito para **versionamento de entidades**, onde você precisa de números monotônicos para optimistic locking e event sourcing, mas **não precisa de unicidade global sem coordenação** (que é o domínio do `Id`).

**Conclusão Rápida:**
- Use **`RegistryVersion`** para: Optimistic locking, event sourcing, audit logs, versões de entidades
- Use **`Id`** para: Primary keys distribuídos, identificadores únicos globais sem coordenação
- Use **ambos juntos**: `Id` para identidade, `RegistryVersion` para versionamento

---

## 📋 Sumário

- [Por Que Usar RegistryVersion ao Invés de Alternativas?](#-por-que-usar-registryversion-ao-invés-de-alternativas)
- [Contexto: Por Que Existe](#-contexto-por-que-existe)
- [Problemas Resolvidos](#-problemas-resolvidos)
  - [Optimistic Locking Sem Round-trips ao Banco](#1-️-optimistic-locking-sem-round-trips-ao-banco)
  - [Ordenação de Sequência de Eventos](#2--ordenação-de-sequência-de-eventos)
  - [Armazenamento Compacto para Versões](#3--armazenamento-compacto-para-versões)
  - [Proteção Contra Clock Drift](#4-️-proteção-contra-clock-drift)
- [Funcionalidades](#-funcionalidades)
- [**⚠️ LIMITAÇÃO CRÍTICA: Clock Skew Futuro**](#️-limitação-crítica-clock-skew-futuro)
- [Como Usar](#-como-usar)
- [Impacto na Performance](#-impacto-na-performance)
  - [Por que tão rápido?](#pergunta-1-por-que-registryversion-é-tão-rápido)
  - [Quando usar RegistryVersion vs Id?](#pergunta-2-quando-usar-registryversion-vs-id)
  - [Metodologia de Benchmarks](#-metodologia-de-benchmarks)
- [Trade-offs](#-tradeoffs)
- [Exemplos Avançados](#-exemplos-avançados)
- [Referências](#-referências)

---

## 🎯 Contexto: Por Que Existe

### O Problema Real

Em aplicações que gerenciam estado mutável, o controle de concorrência é fundamental. As abordagens tradicionais de versionamento apresentam problemas sérios:

**Exemplo de desafios comuns:**

```csharp
❌ Abordagem 1: Versão gerada no banco de dados
public class Order
{
    public Guid Id { get; set; }
    public int Version { get; set; }  // ⚠️ Gerado pelo banco com trigger/computed column
    public decimal Total { get; set; }
}

// Update com optimistic locking:
public async Task UpdateOrder(Order order)
{
    var sql = "UPDATE Orders SET Total = @Total, Version = Version + 1 " +
              "WHERE Id = @Id AND Version = @Version";

    var affected = await _db.ExecuteAsync(sql, order);

    if (affected == 0)
        throw new ConcurrencyException("Version mismatch!");

    // ⚠️ PROBLEMA: Precisa buscar a nova versão do banco!
    order.Version = await _db.QuerySingleAsync<int>(
        "SELECT Version FROM Orders WHERE Id = @Id",
        new { order.Id }
    );
}

❌ Problemas:
- Requer acesso ao banco DUAS VEZES (update + select para buscar nova versão)
- Adiciona latência significativa (~1-5ms por round-trip)
- Dificulta pattern CQRS/Event Sourcing (versão não está disponível antes de persistir)
- Não funciona offline
- Performance limitada pela latência do banco
```

```csharp
❌ Abordagem 2: DateTime.UtcNow como versão
public class Order
{
    public Guid Id { get; set; }
    public DateTime Version { get; set; } = DateTime.UtcNow;  // ⚠️ Pode retroceder!
    public decimal Total { get; set; }
}

// Update:
public async Task UpdateOrder(Order order)
{
    var newVersion = DateTime.UtcNow;

    var sql = "UPDATE Orders SET Total = @Total, Version = @NewVersion " +
              "WHERE Id = @Id AND Version = @Version";

    var affected = await _db.ExecuteAsync(sql,
        new { order.Total, NewVersion = newVersion, order.Id, order.Version });

    if (affected == 0)
        throw new ConcurrencyException("Version mismatch!");

    order.Version = newVersion;
}

❌ Problemas:
- NÃO garante monotonicidade (clock drift pode fazer versão RETROCEDER!)
- Múltiplas atualizações no mesmo milissegundo podem ter MESMA versão
- Resolução limitada a milissegundos (DateTime) ou ticks (DateTime internamente)
- Sem proteção contra ajustes de relógio (NTP, virtualização)
- Difícil de debugar quando versões se repetem
```

```csharp
❌ Abordagem 3: Contador manual com lock
public class VersionGenerator
{
    private static long _counter = 0;
    private static readonly object _lock = new();

    public static long GenerateVersion()
    {
        lock (_lock)  // ⚠️ Contenção entre threads!
        {
            return ++_counter;
        }
    }
}

public class Order
{
    public Guid Id { get; set; }
    public long Version { get; set; } = VersionGenerator.GenerateVersion();
    public decimal Total { get; set; }
}

❌ Problemas:
- Lock causa contenção entre threads (~50-200ns de overhead)
- Performance degrada com mais threads
- Versões são sequenciais globalmente (perde informação temporal)
- Dificulta sistemas distribuídos (múltiplas instâncias geram versões conflitantes)
- Não funciona em cenários offline/desconectados
```

### A Solução

O `RegistryVersion` implementa **versões monotônicas baseadas em UTC ticks** com proteção contra clock drift e **zero overhead de sincronização**.

```csharp
✅ Abordagem com RegistryVersion.GenerateNewVersion():
public class Order
{
    public Id Id { get; private set; } = Id.GenerateNewId();
    public RegistryVersion Version { get; private set; } = RegistryVersion.GenerateNewVersion();
    public decimal Total { get; set; }

    public void UpdateTotal(decimal newTotal)
    {
        Total = newTotal;
        Version = RegistryVersion.GenerateNewVersion();  // ✨ Nova versão local, instantânea!
    }
}

// Update com optimistic locking:
public async Task UpdateOrder(Order order)
{
    var expectedVersion = order.Version;
    order.UpdateTotal(order.Total);  // ✨ Gera nova versão ANTES de persistir!

    var sql = "UPDATE Orders SET Total = @Total, Version = @Version " +
              "WHERE Id = @Id AND Version = @ExpectedVersion";

    var affected = await _db.ExecuteAsync(sql,
        new { order.Total, order.Version, order.Id, ExpectedVersion = expectedVersion });

    if (affected == 0)
        throw new ConcurrencyException("Version mismatch!");

    // ✅ Versão já está atualizada localmente, sem round-trip extra!
}

✅ Benefícios:
- Performance: ~25 nanosegundos por versão (essencialmente grátis!)
- Ordenação: Versões são ordenáveis por timestamp (UTC ticks)
- Tamanho: 8 bytes (50% menor que Guid, mesmo tamanho que long)
- Thread-safe: Sem locks, zero contenção entre threads
- Monotonicidade: Versões de uma thread sempre crescentes
- Compatibilidade: Funciona como long normal (conversão implícita)
- Testabilidade: Suporta TimeProvider para testes com tempo fixo
- Zero round-trips: Versão gerada localmente, sem acesso ao banco
```

**Estrutura do RegistryVersion:**

```
┌──────────────────────────────────────────────────────────────────────────┐
│                   ESTRUTURA DO REGISTRYVERSION (64 bits)                 │
└──────────────────────────────────────────────────────────────────────────┘
│                                                                           │
│                   ┌───────────────────────────────────────┐              │
│    UTC Ticks (64) │         Timestamp completo            │              │
│                   └───────────────────────────────────────┘              │
│                                                                           │
│  UTC Ticks (64 bits):  Número de intervalos de 100ns desde              │
│                        01/01/0001 00:00:00 UTC                           │
│                        → Ordenação temporal precisa                      │
│                        → ~29.000 anos de range                           │
│                        → Resolução: 100 nanosegundos (1 tick)            │
│                                                                           │
│  BENEFÍCIOS:                                                              │
│  - Tamanho: 8 bytes (50% menor que Guid/UUIDv7)                          │
│  - Performance: ~25ns (3x mais rápido que Id.GenerateNewId)              │
│  - Monotônico: Proteção contra clock drift integrada                     │
│  - Compatível: Conversão implícita para/de long e DateTimeOffset         │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 🔧 Problemas Resolvidos

### 1. 🔒 Optimistic Locking Sem Round-trips ao Banco

**Problema:** Versões geradas no banco requerem round-trip extra para obter o novo valor.

#### 📚 Analogia: O Formulário com Selo de Tempo

Imagine que você gerencia documentos em um escritório com múltiplas pessoas editando:

**❌ Com versão gerada no banco:**

```
Você quer atualizar o documento "Contrato-123":

1. Busca documento do arquivo (versão atual: 5)
2. Faz alterações localmente
3. Envia para arquivo: "Atualize Contrato-123 se versão = 5"
4. Arquivo responde: "OK, atualizado para versão 6"
5. ⚠️ PROBLEMA: Você precisa PERGUNTAR ao arquivo qual a nova versão!
6. Envia nova requisição: "Qual versão do Contrato-123?"
7. Arquivo responde: "Versão 6"

Resultado: 3 viagens ao arquivo (buscar, atualizar, buscar nova versão)
Tempo: ~3-15ms (3 round-trips × 1-5ms cada)
```

**✅ Com RegistryVersion:**

```
Você quer atualizar o documento "Contrato-123":

1. Busca documento do arquivo (versão atual: 638123456789000000)
2. Faz alterações localmente
3. ✨ Gera nova versão LOCALMENTE: 638123456789500000
4. Envia para arquivo: "Atualize Contrato-123 se versão = 638123456789000000,
                        nova versão = 638123456789500000"
5. Arquivo responde: "OK, atualizado!"

Resultado: 2 viagens ao arquivo (buscar, atualizar)
Tempo: ~2-10ms (2 round-trips × 1-5ms cada)
Economia: ~1-5ms (33-50% mais rápido!)
```

#### 💻 Impacto Real no Código

**❌ Código com versão do banco:**

```csharp
public class OrderService
{
    public async Task UpdateOrder(Guid orderId, decimal newTotal)
    {
        // Round-trip 1: Buscar entidade
        var order = await _context.Orders.FindAsync(orderId);
        var expectedVersion = order.Version;

        order.Total = newTotal;

        // Round-trip 2: Atualizar (banco incrementa Version automaticamente)
        var updated = await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Orders SET Total = {0}, Version = Version + 1 " +
            "WHERE Id = {1} AND Version = {2}",
            newTotal, orderId, expectedVersion
        );

        if (updated == 0)
            throw new ConcurrencyException();

        // ⚠️ Round-trip 3: Buscar nova versão!
        order.Version = await _context.Orders
            .Where(o => o.Id == orderId)
            .Select(o => o.Version)
            .SingleAsync();

        // TOTAL: 3 round-trips ao banco
        // Tempo estimado: ~3-15ms
    }
}

❌ Problemas:
- 3 round-trips ao banco para uma operação simples
- ~1-5ms de latência adicional (33-50% overhead!)
- Dificulta CQRS (versão não disponível para eventos)
- Não funciona offline
```

**✅ Código com RegistryVersion:**

```csharp
public class Order
{
    public Id Id { get; private set; }
    public RegistryVersion Version { get; private set; }
    public decimal Total { get; private set; }

    public void UpdateTotal(decimal newTotal)
    {
        Total = newTotal;
        Version = RegistryVersion.GenerateNewVersion();  // ✨ Instantâneo (~25ns)!
    }
}

public class OrderService
{
    public async Task UpdateOrder(Guid orderId, decimal newTotal)
    {
        // Round-trip 1: Buscar entidade
        var order = await _context.Orders.FindAsync(orderId);
        var expectedVersion = order.Version;

        // ✨ Gera nova versão LOCALMENTE (~25 nanosegundos)
        order.UpdateTotal(newTotal);

        // Round-trip 2: Atualizar com nova versão
        var updated = await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Orders SET Total = {0}, Version = {1} " +
            "WHERE Id = {2} AND Version = {3}",
            order.Total, order.Version, orderId, expectedVersion
        );

        if (updated == 0)
            throw new ConcurrencyException();

        // ✅ Versão já está correta localmente, sem round-trip extra!

        // TOTAL: 2 round-trips ao banco
        // Tempo estimado: ~2-10ms
        // Economia: ~1-5ms (33-50% mais rápido!)
    }
}

✅ Benefícios:
- Apenas 2 round-trips (buscar + atualizar)
- ~1-5ms mais rápido (economia de 33-50%)
- Versão disponível imediatamente para eventos CQRS
- Funciona offline (pode gerar versões sem banco)
- Código mais limpo e simples
```

**📊 Benchmark Real de Optimistic Locking:**

| Cenário | Round-trips | Latência Estimada | Análise |
|---------|-------------|-------------------|---------|
| **Versão do Banco** | 3 (buscar + atualizar + buscar versão) | ~3-15ms | Overhead de 33-50% |
| **RegistryVersion** | 2 (buscar + atualizar) | ~2-10ms | ✅ **33-50% mais rápido** |

**💡 Economia Real em Alta Carga:**

```
API com 10.000 operações de update por segundo:

Com versão do banco:
  10.000 ops × 3 round-trips = 30.000 queries/seg
  Latência média: ~5ms por operação

Com RegistryVersion:
  10.000 ops × 2 round-trips = 20.000 queries/seg
  Latência média: ~3.3ms por operação

Resultado:
  ✅ 33% menos queries no banco (10.000 queries/seg economizadas!)
  ✅ 33% menos latência (1.7ms economizados por operação)
  ✅ Melhor utilização de recursos (CPU, conexões, memória)
```

---

### 2. 📊 Ordenação de Sequência de Eventos

**Problema:** Em event sourcing, precisamos de números de sequência monotônicos que reflitam a ordem exata dos eventos.

#### 🎬 Cenário Crítico: Event Sourcing com Replay

Imagine um sistema de e-commerce com Event Sourcing:

**❌ Com timestamp manual (DateTime.UtcNow):**

```csharp
// Eventos originais (primeira execução)
public class OrderEvent
{
    public Guid OrderId { get; set; }
    public long Sequence { get; set; }  // ⚠️ Gerado manualmente
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Execução 1 (produção):
var event1 = new OrderCreatedEvent
{
    OrderId = orderId,
    Sequence = 1,  // ⚠️ Manual, propenso a erros
    Timestamp = DateTime.UtcNow  // 2025-01-15 10:30:00.123
};

var event2 = new OrderItemAddedEvent
{
    OrderId = orderId,
    Sequence = 2,  // ⚠️ Depende de controle manual
    Timestamp = DateTime.UtcNow  // 2025-01-15 10:30:00.123 (MESMO timestamp!)
};

var event3 = new OrderPaidEvent
{
    OrderId = orderId,
    Sequence = 3,
    Timestamp = DateTime.UtcNow  // 2025-01-15 10:30:00.124
};

❌ Problemas:
- Sequência MANUAL (esqueceu de incrementar? Bug!)
- Timestamps podem REPETIR (mesma resolução de milissegundo)
- Clock drift pode fazer evento posterior ter timestamp ANTERIOR
- Ordenação por timestamp não reflete ordem real de criação
- Difícil garantir ordem em replay
```

**✅ Com RegistryVersion (sequence monotônica):**

```csharp
public class OrderEvent
{
    public Guid OrderId { get; set; }
    public RegistryVersion Sequence { get; set; }  // ✨ Gerado automaticamente
}

// Execução 1 (produção):
var event1 = new OrderCreatedEvent
{
    OrderId = orderId,
    Sequence = RegistryVersion.GenerateNewVersion()  // 638401234567890000
};

var event2 = new OrderItemAddedEvent
{
    OrderId = orderId,
    Sequence = RegistryVersion.GenerateNewVersion()  // 638401234567890001 (SEMPRE maior!)
};

var event3 = new OrderPaidEvent
{
    OrderId = orderId,
    Sequence = RegistryVersion.GenerateNewVersion()  // 638401234567890002 (SEMPRE maior!)
};

// Replay 3 meses depois:
var events = await _eventStore.GetEvents(orderId);
var orderedEvents = events.OrderBy(e => e.Sequence).ToList();

// ✅ Ordem SEMPRE preservada!
// event1.Sequence < event2.Sequence < event3.Sequence (garantido!)

✅ Benefícios:
- Sequência AUTOMÁTICA (sem controle manual)
- SEMPRE monotônica (cada versão é maior que a anterior na mesma thread)
- Proteção contra clock drift (incrementa se relógio retroceder)
- Ordenação GARANTIDA em replays
- Resolução de 100 nanosegundos (10.000x melhor que milissegundos)
- Performance: ~25ns por sequência
```

**📊 Comparação: Sequências em Event Sourcing**

| Aspecto | DateTime.UtcNow | Contador Manual | RegistryVersion |
|---------|-----------------|-----------------|-----------------|
| **Monotônico?** | ❌ Não (clock drift) | ✅ Sim | ✅ Sim |
| **Automático?** | ✅ Sim | ❌ Não (manual) | ✅ Sim |
| **Resolução** | ~1ms (pode repetir) | N/A | 100ns (nunca repete) |
| **Clock drift protection** | ❌ Não | N/A | ✅ Sim |
| **Performance** | ~25ns | ~50-200ns (lock) | ~25ns |
| **Ordenação garantida** | ❌ Não | ✅ Sim | ✅ Sim |

---

### 3. 💾 Armazenamento Compacto para Versões

**Problema:** Usar Guid como versão ocupa 16 bytes, dobrando o espaço necessário para versionamento.

#### 📦 Impacto Real no Armazenamento

**❌ Com Guid como versão:**

```csharp
public class Order
{
    public Guid Id { get; set; }       // 16 bytes
    public Guid Version { get; set; }   // 16 bytes ⚠️ Desperdiça espaço!
    public decimal Total { get; set; }  // 16 bytes
    // ... outros campos
}

// Tamanho dos identificadores: 16 + 16 = 32 bytes

// 1 milhão de registros:
// Só em Id + Version: 32 MB
// Com índices (primário + versão): ~64-96 MB
```

**✅ Com RegistryVersion:**

```csharp
public class Order
{
    public Guid Id { get; set; }              // 16 bytes
    public long Version { get; set; }          // 8 bytes ✅ 50% menor!
    public decimal Total { get; set; }         // 16 bytes
    // ... outros campos
}

// Tamanho dos identificadores: 16 + 8 = 24 bytes

// 1 milhão de registros:
// Só em Id + Version: 24 MB
// Com índices: ~48-72 MB
// Economia: ~16-24 MB (25-33% menos espaço!)
```

**📊 Comparação de Espaço:**

| Registros | Guid Version | RegistryVersion | Economia |
|-----------|--------------|-----------------|----------|
| 1 milhão | 32 MB | 24 MB | **8 MB (25%)** |
| 10 milhões | 320 MB | 240 MB | **80 MB (25%)** |
| 100 milhões | 3.2 GB | 2.4 GB | **800 MB (25%)** |
| 1 bilhão | 32 GB | 24 GB | **8 GB (25%)** |

**💡 Benefícios Adicionais:**

1. **Índices menores**: Menos I/O, melhor cache hit rate
2. **Memória economizada**: 25% menos RAM para carregar dados
3. **Network transfer**: 25% menos dados trafegados
4. **Backup/restore**: 25% menos espaço e tempo

---

### 4. ⏱️ Proteção Contra Clock Drift

**Problema:** Relógios de sistema podem retroceder (NTP sync, virtualização, bugs), quebrando monotonicidade.

```csharp
❌ Implementação ingênua com timestamp:
public static long GenerateVersion()
{
    return DateTime.UtcNow.Ticks;
}

// Gerando versões:
var v1 = GenerateVersion();  // ticks: 638401234567890000
Thread.Sleep(5);
// ⚠️ Relógio retrocede (NTP sync, virtualização, bug)
var v2 = GenerateVersion();  // ticks: 638401234567880000  ❌ MENOR que v1!

❌ Problemas:
- Versões não são monotônicas (v2 < v1)
- Quebra ordenação esperada
- Pode causar bugs em optimistic locking
- Difícil de debugar (acontece raramente)
```

**Solução:** Proteção contra clock drift integrada.

```csharp
✅ RegistryVersion.GenerateNewVersion() com proteção:
public static RegistryVersion GenerateNewVersion()
{
    long ticks = DateTimeOffset.UtcNow.UtcTicks;

    // Proteção contra clock drift:
    // Se o timestamp atual for menor ou igual ao último válido,
    // incrementamos em 1 tick (100ns) para garantir monotonicidade.
    if (ticks <= _lastTicks)
        ticks = _lastTicks + 1;  // ✨ Incrementa!

    _lastTicks = ticks;
    return new RegistryVersion(ticks);
}

// Gerando versões:
var v1 = RegistryVersion.GenerateNewVersion();  // ticks: 638401234567890000
Thread.Sleep(5);
// ⚠️ Relógio retrocede
var v2 = RegistryVersion.GenerateNewVersion();  // ticks: 638401234567890001 ✅ MAIOR que v1!

✅ Benefícios:
- Versões SEMPRE monotônicas por thread
- Proteção automática contra clock drift
- Comportamento previsível
- Nenhuma configuração necessária
```

---

## ✨ Funcionalidades

### ⚡ Performance Extrema

Geração ultrarrápida de versões sem alocações no heap.

```csharp
var version = RegistryVersion.GenerateNewVersion();  // ~25 nanosegundos
```

**Por quê é rápido?**
- `ThreadStatic`: Zero contenção entre threads
- Estrutura simples: Apenas um `long` (8 bytes)
- Sem geração de randomness: Não precisa de `RandomNumberGenerator.Fill()`
- Operações mínimas: Comparação de ticks + incremento condicional
- **3x mais rápido que Id.GenerateNewId()** (~25ns vs ~73ns)

---

### 🔐 Thread-Safe Sem Locks

Cada thread mantém seu próprio estado, eliminando contenção.

```csharp
// Gerar milhões de versões em paralelo:
Parallel.For(0, 10_000_000, i =>
{
    var version = RegistryVersion.GenerateNewVersion();  // Zero contenção!
    ProcessEvent(version);
});
```

**Como funciona:**
- `[ThreadStatic]` faz cada thread ter sua própria variável `_lastTicks`
- Thread A: `_lastTicks` (cópia independente)
- Thread B: `_lastTicks` (cópia independente)
- Sem necessidade de sincronização

---

### 📅 Ordenação Temporal

Versões são ordenáveis pelo timestamp embutido.

```csharp
var v1 = RegistryVersion.GenerateNewVersion();
Thread.Sleep(10);  // Espera 10ms
var v2 = RegistryVersion.GenerateNewVersion();

Assert.True(v1 < v2);  // ✅ v1 foi gerado antes
```

**Benefícios:**
- Ordenação natural por tempo de criação
- Debugging facilitado (sabe ordem de modificações)
- Event sourcing funciona perfeitamente
- Audit logs ordenados automaticamente

---

### 🛡️ Proteção Contra Clock Drift

Mantém monotonicidade mesmo se o relógio retroceder.

```csharp
// Mesmo com clock drift, versões nunca retrocessam:
var v1 = RegistryVersion.GenerateNewVersion();  // ticks: 1000
// ⚠️ Relógio retrocede
var v2 = RegistryVersion.GenerateNewVersion();  // ticks: 1001 ✅ Ainda maior!

Assert.True(v2 > v1);  // ✅ Sempre monotônico
```

**Como funciona:**
- Detecta quando `ticks <= _lastTicks`
- Incrementa em 1 tick (100 nanosegundos)
- Garante monotonicidade por thread

---

### 💾 Tamanho Compacto (8 bytes)

50% menor que Guid, economizando espaço em disco, memória e rede.

```csharp
sizeof(RegistryVersion) == sizeof(long) == 8 bytes
sizeof(Guid) == 16 bytes

// Economia: 50% de espaço em campos de versão
```

---

### 🔄 Compatível com long e DateTimeOffset

Conversão implícita para/de long e DateTimeOffset.

```csharp
// RegistryVersion → long (implícito)
RegistryVersion version = RegistryVersion.GenerateNewVersion();
long ticks = version;  // ✅ Conversão automática

// long → RegistryVersion (implícito)
long existingTicks = 638401234567890000;
RegistryVersion parsedVersion = existingTicks;  // ✅ Conversão automática

// RegistryVersion → DateTimeOffset
DateTimeOffset timestamp = version.AsDateTimeOffset;
Console.WriteLine(timestamp);  // 2025-01-15T10:30:00.000Z
```

---

## ⚠️ LIMITAÇÃO CRÍTICA: Clock Skew Futuro

### 🚨 Problema: Relógio Configurado para o Futuro

**Severidade:** Alta para padrões específicos de uso

**Descrição do Problema:**

Se o relógio do sistema for configurado para uma data no futuro e depois corrigido para a data atual, **todas as versões geradas durante o período "futuro" serão permanentemente maiores que todas as versões subsequentes**, quebrando padrões que dependem de "maior versão = mais recente".

### 📖 Cenário de Exemplo

```csharp
// CENÁRIO PROBLEMÁTICO:

// 1. Servidor com relógio configurado para 100 anos no futuro (2125)
//    (acidentalmente ou por erro de configuração)
DateTimeOffset.UtcNow;  // 2125-01-15 (FUTURO!)

var v1 = RegistryVersion.GenerateNewVersion();
// v1.Value = 703847234567890000  (ticks de 2125)
// v1.AsDateTimeOffset = 2125-01-15T10:30:00Z

// 2. Relógio é corrigido para data atual (2025)
DateTimeOffset.UtcNow;  // 2025-01-15 (ATUAL)

var v2 = RegistryVersion.GenerateNewVersion();
// v2.Value = 638401234567890000  (ticks de 2025)
// v2.AsDateTimeOffset = 2025-01-15T10:30:00Z

// 3. RESULTADO PERMANENTE:
Console.WriteLine(v1 > v2);  // ✅ True - v1 é "maior" para sempre!

// ⚠️ PROBLEMA: v1 foi gerada ANTES de v2 no tempo real,
// mas tem timestamp MAIOR (100 anos no futuro).
// Isso quebra permanentemente qualquer lógica que use
// "versão maior = mais recente"
```

### 💥 Impacto por Padrão de Uso

| Padrão de Uso | Impacto | Análise |
|---------------|---------|---------|
| **✅ Optimistic Locking (comparação exata)** | **SEM IMPACTO** | `WHERE Version = @Expected` compara exatamente, não depende de ordenação |
| **❌ "Highest Version Wins"** | **QUEBRA PERMANENTE** | Versão futura sempre "ganha", mesmo sendo antiga |
| **❌ Event Sourcing (ordenação por sequência)** | **CORROMPE ESTADO** | Eventos fora de ordem corrompem aggregate |
| **❌ Cache Invalidation (versão mais recente)** | **CACHE NUNCA EXPIRA** | Versão futura nunca será substituída |
| **❌ CQRS Read Models (comparação de versão)** | **PARA DE ATUALIZAR** | Read model nunca aceita versões "menores" |
| **❌ Merge/Sync de Estados Distribuídos** | **ESTADO INCORRETO** | Estado "futuro" sempre prevalece |

### 🔴 Padrões VULNERÁVEIS (Evitar ou Validar)

```csharp
// ❌ PADRÃO VULNERÁVEL 1: "Highest Version Wins"
public class StateManager
{
    public void MergeStates(List<State> states)
    {
        // ⚠️ VULNERÁVEL: Se algum state tem versão futura,
        // ele SEMPRE será escolhido, mesmo sendo antigo!
        var latest = states.OrderByDescending(s => s.Version).First();
        _currentState = latest;  // ❌ PODE SER ESTADO ANTIGO!
    }
}

// ❌ PADRÃO VULNERÁVEL 2: "Newer Version" Check
public class CacheManager
{
    public void UpdateCache(string key, object value, RegistryVersion version)
    {
        var current = _cache.Get<CachedItem>(key);

        // ⚠️ VULNERÁVEL: Versão futura NUNCA será substituída
        if (current == null || version > current.Version)
        {
            _cache.Set(key, new CachedItem { Value = value, Version = version });
        }
        // Se current.Version é do "futuro", nenhum update novo funcionará!
    }
}

// ❌ PADRÃO VULNERÁVEL 3: Event Sourcing com Ordenação por Sequência
public class OrderAggregate
{
    public static OrderAggregate LoadFromHistory(IEnumerable<OrderEvent> events)
    {
        var aggregate = new OrderAggregate();

        // ⚠️ VULNERÁVEL: Se algum evento tem sequência futura,
        // ordenação estará INCORRETA
        foreach (var evt in events.OrderBy(e => e.Sequence))
        {
            aggregate.Apply(evt);  // ❌ ORDEM ERRADA = ESTADO CORROMPIDO
        }

        return aggregate;
    }
}

// ❌ PADRÃO VULNERÁVEL 4: CQRS Read Model Updates
public class OrderReadModelUpdater
{
    public async Task Handle(OrderUpdatedEvent evt)
    {
        var readModel = await _db.OrderReadModels.FindAsync(evt.OrderId);

        // ⚠️ VULNERÁVEL: Se readModel.Version é futura,
        // NENHUM update novo será aplicado!
        if (readModel.Version < evt.Version)
        {
            readModel.Update(evt);
            readModel.Version = evt.Version;
        }
        // Se Version atual é do "futuro", read model para de atualizar permanentemente!
    }
}
```

### ✅ Padrões SEGUROS (Não Afetados)

```csharp
// ✅ PADRÃO SEGURO 1: Optimistic Locking com Comparação Exata
public async Task UpdateOrder(Guid orderId, decimal newTotal)
{
    var order = await _context.Orders.FindAsync(orderId);
    var expectedVersion = order.Version;

    order.UpdateTotal(newTotal);  // Gera nova versão localmente

    // ✅ SEGURO: Compara versão EXATA, não depende de "maior/menor"
    var updated = await _context.Database.ExecuteSqlRawAsync(
        "UPDATE Orders SET Total = {0}, Version = {1} " +
        "WHERE Id = {2} AND Version = {3}",  // ✅ Comparação exata!
        order.Total, order.Version, orderId, expectedVersion
    );

    if (updated == 0)
        throw new ConcurrencyException("Version mismatch!");
}

// ✅ PADRÃO SEGURO 2: Entity Framework Core ConcurrencyToken
public class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.Version)
                .IsConcurrencyToken();  // ✅ EF Core usa comparação EXATA
        });
    }
}

// ✅ PADRÃO SEGURO 3: Validação de Timestamp Antes de Comparação
public class StateManager
{
    public void MergeStates(List<State> states)
    {
        // ✅ Validar timestamps antes de usar
        var validStates = states.Where(s =>
        {
            var timestamp = s.Version.AsDateTimeOffset;
            var now = DateTimeOffset.UtcNow;
            var maxFuture = now.AddMinutes(5);

            // Rejeitar versões muito no futuro
            if (timestamp > maxFuture)
            {
                _logger.LogWarning(
                    "State has future timestamp: {Timestamp}, current: {Now}",
                    timestamp, now
                );
                return false;
            }

            return true;
        }).ToList();

        // Agora é seguro usar ordenação
        var latest = validStates.OrderByDescending(s => s.Version).First();
        _currentState = latest;
    }
}
```

### 🛡️ Estratégias de Mitigação

#### 1️⃣ **Prevenção: Sincronização de Relógio**

**Recomendação:** Use sincronização automática de relógio em TODOS os ambientes.

```bash
# Cloud Providers (Automático)
# AWS: Amazon Time Sync Service (automático em EC2)
# Azure: Azure Time Sync (automático em VMs)
# GCP: Google NTP (automático em Compute Engine)

# Linux (On-Premise)
sudo timedatectl set-ntp true
systemctl enable systemd-timesyncd

# Windows (On-Premise)
# Habilitar sincronização automática via NTP
w32tm /config /manualpeerlist:"pool.ntp.org" /syncfromflags:manual /update
w32tm /resync

# Docker/Kubernetes
# Sincroniza automaticamente com host time
# Verificar: docker run --rm alpine date
```

#### 2️⃣ **Detecção: Validação de Timestamp**

**Recomendação:** Valide versões antes de operações críticas.

```csharp
/// <summary>
/// Helper para validar que versões não estão no futuro.
/// </summary>
public static class RegistryVersionValidator
{
    /// <summary>
    /// Valida que a versão não está muito no futuro.
    /// </summary>
    /// <param name="version">Versão a validar</param>
    /// <param name="tolerance">Tolerância para timestamps futuros (padrão: 5 minutos)</param>
    /// <exception cref="InvalidOperationException">Se versão está muito no futuro</exception>
    public static void ValidateNotFuture(
        RegistryVersion version,
        TimeSpan? tolerance = null)
    {
        var maxFutureTolerance = tolerance ?? TimeSpan.FromMinutes(5);
        var versionTime = version.AsDateTimeOffset;
        var now = DateTimeOffset.UtcNow;
        var maxAllowed = now.Add(maxFutureTolerance);

        if (versionTime > maxAllowed)
        {
            throw new InvalidOperationException(
                $"Version timestamp is in the future. " +
                $"Version time: {versionTime:O}, " +
                $"Current time: {now:O}, " +
                $"Difference: {(versionTime - now).TotalMinutes:F1} minutes. " +
                $"This may indicate clock skew or system misconfiguration."
            );
        }
    }

    /// <summary>
    /// Valida que a versão está dentro de um range razoável (não muito antiga, nem futura).
    /// </summary>
    public static void ValidateReasonable(
        RegistryVersion version,
        TimeSpan? maxAge = null,
        TimeSpan? maxFuture = null)
    {
        var versionTime = version.AsDateTimeOffset;
        var now = DateTimeOffset.UtcNow;

        // Verificar se não está muito no futuro
        var futureTolerance = maxFuture ?? TimeSpan.FromMinutes(5);
        if (versionTime > now.Add(futureTolerance))
        {
            throw new InvalidOperationException(
                $"Version is too far in the future: {versionTime:O} " +
                $"(current: {now:O}, diff: {(versionTime - now).TotalHours:F1}h)"
            );
        }

        // Verificar se não está muito antiga
        var ageTolerance = maxAge ?? TimeSpan.FromDays(365);
        if (versionTime < now.Subtract(ageTolerance))
        {
            throw new InvalidOperationException(
                $"Version is too old: {versionTime:O} " +
                $"(current: {now:O}, age: {(now - versionTime).TotalDays:F0} days)"
            );
        }
    }
}

// USO: Em padrões vulneráveis
public class OrderReadModelUpdater
{
    public async Task Handle(OrderUpdatedEvent evt)
    {
        // ✅ Validar ANTES de usar em comparação
        RegistryVersionValidator.ValidateNotFuture(evt.Version);

        var readModel = await _db.OrderReadModels.FindAsync(evt.OrderId);

        // Agora é seguro comparar
        if (readModel.Version < evt.Version)
        {
            readModel.Update(evt);
            readModel.Version = evt.Version;
        }
    }
}
```

#### 3️⃣ **Monitoramento: Detectar Clock Drift**

**Recomendação:** Monitore drift de relógio para detectar problemas antes que causem corrupção.

```csharp
/// <summary>
/// Monitora clock health durante geração de versões.
/// </summary>
public static class RegistryVersionMonitoring
{
    private static readonly ILogger _logger =
        LoggerFactory.CreateLogger("RegistryVersionMonitoring");

    /// <summary>
    /// Gera versão com monitoramento de clock drift.
    /// </summary>
    public static RegistryVersion GenerateWithMonitoring()
    {
        var beforeGen = DateTimeOffset.UtcNow;
        var version = RegistryVersion.GenerateNewVersion();
        var versionTime = version.AsDateTimeOffset;

        var driftMs = (versionTime - beforeGen).TotalMilliseconds;

        // Log para drift suspeito (> 1 segundo)
        if (Math.Abs(driftMs) > 1000)
        {
            _logger.LogWarning(
                "Clock drift detected: {Drift}ms. " +
                "Version timestamp: {VersionTime}, Expected: {ExpectedTime}",
                driftMs, versionTime, beforeGen
            );
        }

        // Alert CRÍTICO para drift futuro significativo (> 1 minuto)
        if (driftMs > 60_000)
        {
            _logger.LogCritical(
                "CRITICAL: Clock appears to be {Drift}ms ({DriftMinutes:F1} minutes) in the future! " +
                "Version: {VersionTime}, Current: {CurrentTime}. " +
                "This may corrupt version ordering. Investigate immediately!",
                driftMs, driftMs / 60_000, versionTime, beforeGen
            );

            // Opcional: Emitir métrica para alertas
            Metrics.Gauge("registryversion.clock_drift_ms", driftMs);
        }

        return version;
    }
}

// Configurar alertas (exemplo Prometheus)
// ALERTA se registryversion.clock_drift_ms > 60000 (1 minuto)
```

#### 4️⃣ **Recuperação: Quando Detectar Problema**

Se você detectar versões futuras já geradas:

```csharp
/// <summary>
/// Detecta e reporta versões suspeitas em dados existentes.
/// </summary>
public class RegistryVersionAudit
{
    public async Task<List<SuspiciousVersion>> AuditVersions()
    {
        var suspicious = new List<SuspiciousVersion>();
        var now = DateTimeOffset.UtcNow;
        var maxFuture = now.AddMinutes(5);

        // Auditar todas as entidades com versões
        var orders = await _db.Orders.ToListAsync();

        foreach (var order in orders)
        {
            var versionTime = order.Version.AsDateTimeOffset;

            if (versionTime > maxFuture)
            {
                suspicious.Add(new SuspiciousVersion
                {
                    EntityId = order.Id,
                    EntityType = "Order",
                    Version = order.Version,
                    VersionTime = versionTime,
                    CurrentTime = now,
                    DriftMinutes = (versionTime - now).TotalMinutes
                });
            }
        }

        return suspicious;
    }

    /// <summary>
    /// Corrige versões futuras regenerando com timestamp atual.
    /// ATENÇÃO: Só use em casos extremos, pode quebrar optimistic locking ativo!
    /// </summary>
    public async Task FixFutureVersions(List<Guid> entityIds)
    {
        foreach (var id in entityIds)
        {
            var order = await _db.Orders.FindAsync(id);

            // Regenerar versão com timestamp atual
            var oldVersion = order.Version;
            order.Version = RegistryVersion.GenerateNewVersion();

            _logger.LogWarning(
                "Fixed future version for Order {OrderId}. " +
                "Old: {OldVersion} ({OldTime}), " +
                "New: {NewVersion} ({NewTime})",
                id, oldVersion, oldVersion.AsDateTimeOffset,
                order.Version, order.Version.AsDateTimeOffset
            );
        }

        await _db.SaveChangesAsync();
    }
}
```

### 📊 Probabilidade de Ocorrência por Ambiente

| Ambiente | Probabilidade | Risco | Recomendação |
|----------|---------------|-------|--------------|
| **Cloud (AWS/Azure/GCP)** | ⬛ Muito Baixa | 🟢 Baixo | NTP automático, monitorar apenas |
| **On-Premise com NTP** | ⬛⬛ Baixa | 🟢 Baixo | Validar configuração NTP |
| **Containers (Docker/K8s)** | ⬛⬛ Baixa-Média | 🟡 Médio | Sincroniza com host, verificar host |
| **VMs (VMware/Hyper-V)** | ⬛⬛⬛ Média | 🟡 Médio | Cuidado com snapshots/migrations |
| **Desenvolvimento/Testes** | ⬛⬛⬛⬛ Média-Alta | 🟠 Alto | Devs podem mudar relógio, validar |
| **Edge/IoT sem NTP** | ⬛⬛⬛⬛⬛ Alta | 🔴 Muito Alto | SEMPRE validar, monitorar |
| **Air-gapped Systems** | ⬛⬛⬛⬛⬛ Muito Alta | 🔴 Muito Alto | Validação obrigatória |

### 💡 Quando Preocupar vs Quando NÃO Preocupar

#### ✅ Você PODE USAR sem preocupação se:

1. **Usa apenas Optimistic Locking (comparação exata)**
   ```csharp
   WHERE Id = @Id AND Version = @ExpectedVersion  // ✅ Seguro!
   ```

2. **Está em ambiente cloud com NTP automático**
   - AWS, Azure, GCP têm sincronização automática e confiável
   - Probabilidade de drift > 1 ano é astronômica

3. **Não usa padrões "highest version wins"**
   - Não ordena por versão para escolher "mais recente"
   - Não compara versões para decidir qual estado prevalece

#### ⚠️ Você DEVE VALIDAR se:

1. **Usa "highest version wins" ou comparação de versões**
   ```csharp
   if (newVersion > currentVersion) { ... }  // ⚠️ Validar!
   ```

2. **Está em ambiente edge/IoT sem NTP confiável**
   - Dispositivos sem acesso à internet
   - Bateria de backup pode falhar
   - Relógio pode resetar para data padrão (ex: 01/01/2000 ou ano futuro)

3. **Usa Event Sourcing com ordenação por sequência**
   ```csharp
   events.OrderBy(e => e.Sequence)  // ⚠️ Validar!
   ```

4. **Tem ambientes de desenvolvimento/teste**
   - Desenvolvedores podem mudar relógio para testes
   - Validar em staging antes de produção

### 📝 Checklist de Segurança

```markdown
✅ Configurar NTP/PTP em todos os servidores
✅ Monitorar clock drift (alertas para > 1 minuto)
✅ Validar versões em padrões "highest version wins"
✅ Auditar versões suspeitas periodicamente
✅ Testar recuperação de clock skew em staging
✅ Documentar procedimentos de recuperação
✅ Treinar equipe sobre o problema
```

### 🎓 Comparação: RegistryVersion vs Id

**Importante:** `Id.GenerateNewId()` tem **vulnerabilidade similar** mas com impacto diferente:

| Aspecto | RegistryVersion | Id (UUIDv7) |
|---------|-----------------|-------------|
| **Usa timestamp?** | ✅ Sim (UTC ticks) | ✅ Sim (milissegundos) |
| **Vulnerável a clock futuro?** | ✅ Sim | ✅ Sim |
| **Impacto em ordenação?** | ⚠️ Quebra se usado para "highest wins" | ⚠️ Quebra ordenação temporal |
| **Impacto em unicidade?** | N/A (não garante unicidade global) | ✅ Ainda único (46 bits random) |
| **Uso principal** | Versioning (comparação exata OK) | Primary keys (ordenação não crítica) |

**Conclusão:** Ambos devem ter relógio sincronizado, mas `RegistryVersion` é mais sensível em padrões de "highest version wins".

---

## 📖 Como Usar

### 1️⃣ Uso Básico - Geração Simples

```csharp
using PragmaStack.Core.RegistryVersions;

// Gerar uma nova versão
var version = RegistryVersion.GenerateNewVersion();
Console.WriteLine($"Version: {version.Value}");
// Saída: Version: 638401234567890000

// Acessar o long interno
long ticks = version.Value;
Console.WriteLine(ticks.ToString());
```

**Quando usar:** Qualquer situação onde você precisa de um número de versão monotônico.

---

### 2️⃣ Uso em Entidades de Domínio (Optimistic Locking)

```csharp
public class Order
{
    public Id Id { get; private set; } = Id.GenerateNewId();
    public RegistryVersion Version { get; private set; } = RegistryVersion.GenerateNewVersion();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal Total { get; private set; }

    public void UpdateTotal(decimal newTotal)
    {
        Total = newTotal;
        Version = RegistryVersion.GenerateNewVersion();  // ✨ Nova versão
    }
}

// Uso:
var order = new Order();
Console.WriteLine($"Order ID: {order.Id}, Version: {order.Version}");

order.UpdateTotal(150.00m);
Console.WriteLine($"Updated! New Version: {order.Version}");
```

**Quando usar:** Entidades de domínio que precisam de optimistic locking.

---

### 3️⃣ Optimistic Locking com Entity Framework Core

```csharp
// Entidade
public class Product
{
    public Id Id { get; private set; } = Id.GenerateNewId();
    public RegistryVersion Version { get; private set; } = RegistryVersion.GenerateNewVersion();
    public string Name { get; set; }
    public decimal Price { get; private set; }

    public void UpdatePrice(decimal newPrice)
    {
        Price = newPrice;
        Version = RegistryVersion.GenerateNewVersion();
    }
}

// DbContext
public class AppDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Configurar conversão RegistryVersion ↔ long
            entity.Property(e => e.Version)
                .HasConversion(
                    version => version.Value,              // RegistryVersion → long (para banco)
                    ticks => RegistryVersion.FromLong(ticks)    // long → RegistryVersion (do banco)
                )
                .IsConcurrencyToken();  // ✨ Optimistic locking automático!
        });
    }
}

// Service com optimistic locking
public class ProductService
{
    private readonly AppDbContext _context;

    public async Task UpdateProductPrice(Guid productId, decimal newPrice)
    {
        var product = await _context.Products.FindAsync(productId);

        if (product == null)
            throw new NotFoundException();

        // ✨ Gera nova versão ANTES de salvar
        product.UpdatePrice(newPrice);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Product was modified by another user!");
        }
    }
}
```

**Quando usar:** Persistência com Entity Framework Core que precisa de optimistic locking.

---

### 4️⃣ Event Sourcing com Sequências Monotônicas

```csharp
// Evento base
public abstract class OrderEvent
{
    public Id EventId { get; set; } = Id.GenerateNewId();
    public Guid AggregateId { get; set; }
    public RegistryVersion Sequence { get; set; }  // ✨ Sequência monotônica
    public DateTime Timestamp { get; set; }
}

// Eventos específicos
public class OrderCreatedEvent : OrderEvent
{
    public decimal Total { get; set; }
}

public class OrderItemAddedEvent : OrderEvent
{
    public string ProductName { get; set; }
    public decimal Price { get; set; }
}

public class OrderPaidEvent : OrderEvent
{
    public string PaymentMethod { get; set; }
}

// Aggregate com Event Sourcing
public class OrderAggregate
{
    private readonly List<OrderEvent> _uncommittedEvents = new();

    public Guid Id { get; private set; }
    public RegistryVersion CurrentSequence { get; private set; }
    public decimal Total { get; private set; }

    public static OrderAggregate Create(decimal initialTotal)
    {
        var aggregate = new OrderAggregate { Id = Guid.NewGuid() };

        aggregate.RaiseEvent(new OrderCreatedEvent
        {
            AggregateId = aggregate.Id,
            Sequence = RegistryVersion.GenerateNewVersion(),  // ✨ Sequência automática
            Total = initialTotal,
            Timestamp = DateTime.UtcNow
        });

        return aggregate;
    }

    public void AddItem(string productName, decimal price)
    {
        RaiseEvent(new OrderItemAddedEvent
        {
            AggregateId = Id,
            Sequence = RegistryVersion.GenerateNewVersion(),  // ✨ Sempre maior que anterior
            ProductName = productName,
            Price = price,
            Timestamp = DateTime.UtcNow
        });
    }

    private void RaiseEvent(OrderEvent @event)
    {
        Apply(@event);
        CurrentSequence = @event.Sequence;
        _uncommittedEvents.Add(@event);
    }

    private void Apply(OrderEvent @event)
    {
        switch (@event)
        {
            case OrderCreatedEvent created:
                Total = created.Total;
                break;
            case OrderItemAddedEvent itemAdded:
                Total += itemAdded.Price;
                break;
        }
    }

    public IReadOnlyList<OrderEvent> GetUncommittedEvents() => _uncommittedEvents;
}

// Uso:
var order = OrderAggregate.Create(100.00m);
order.AddItem("Notebook", 3500.00m);
order.AddItem("Mouse", 50.00m);

var events = order.GetUncommittedEvents();
// events[0].Sequence < events[1].Sequence < events[2].Sequence ✅ Garantido!
```

**Quando usar:** Event Sourcing onde ordem de eventos é crítica.

---

### 5️⃣ Uso com TimeProvider (Testabilidade)

O `RegistryVersion.GenerateNewVersion()` suporta injeção de `TimeProvider`, permitindo testes completamente determinísticos com tempo fixo.

#### Teste com Tempo Fixo

```csharp
using PragmaStack.Core.RegistryVersions;
using PragmaStack.Core.TimeProviders;

[Fact]
public void TestOptimisticLocking_WithFixedTime()
{
    // Arrange - Configurar tempo fixo para testes determinísticos
    var fixedTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
    var timeProvider = new CustomTimeProvider(
        utcNowFunc: _ => fixedTime,
        localTimeZone: null
    );

    // Act - Gerar versões com tempo fixo
    var v1 = RegistryVersion.GenerateNewVersion(timeProvider);
    var v2 = RegistryVersion.GenerateNewVersion(timeProvider);
    var v3 = RegistryVersion.GenerateNewVersion(timeProvider);

    // Assert
    // ✅ Todos terão o mesmo timestamp base, mas ticks incrementados
    Assert.True(v1 < v2);
    Assert.True(v2 < v3);

    // ✅ Versões são determinísticas e repetíveis
    // Rodando o teste novamente, os valores serão idênticos
}
```

#### Teste com Tempo Avançando

```csharp
[Fact]
public void TestEventSequence_WithAdvancingTime()
{
    // Arrange - Simular passagem de tempo
    var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
    var currentTime = baseTime;
    var timeProvider = new CustomTimeProvider(
        utcNowFunc: _ => currentTime,
        localTimeZone: null
    );

    // Act - Criar eventos em diferentes momentos
    var event1 = new OrderCreatedEvent
    {
        Sequence = RegistryVersion.GenerateNewVersion(timeProvider)
    };

    // Avançar 5 milissegundos
    currentTime = baseTime.AddMilliseconds(5);

    var event2 = new OrderItemAddedEvent
    {
        Sequence = RegistryVersion.GenerateNewVersion(timeProvider)
    };

    // Avançar mais 10 milissegundos
    currentTime = baseTime.AddMilliseconds(15);

    var event3 = new OrderPaidEvent
    {
        Sequence = RegistryVersion.GenerateNewVersion(timeProvider)
    };

    // Assert - Verificar ordenação temporal
    Assert.True(event1.Sequence < event2.Sequence);
    Assert.True(event2.Sequence < event3.Sequence);

    // ✅ Ordem dos eventos é garantida e determinística
}
```

#### Teste de Clock Drift

```csharp
[Fact]
public void TestMonotonicity_WhenClockGoesBackward()
{
    // Arrange - Simular problema de clock drift
    var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
    var currentTime = baseTime;
    var timeProvider = new CustomTimeProvider(
        utcNowFunc: _ => currentTime,
        localTimeZone: null
    );

    // Act - Gerar versão no tempo normal
    var v1 = RegistryVersion.GenerateNewVersion(timeProvider);

    // Simular relógio retroagindo 10ms
    currentTime = baseTime.AddMilliseconds(-10);

    var v2 = RegistryVersion.GenerateNewVersion(timeProvider);
    var v3 = RegistryVersion.GenerateNewVersion(timeProvider);

    // Assert - Mesmo com clock drift, monotonicidade é mantida
    Assert.True(v1 < v2, "v1 deve ser menor que v2 mesmo com clock drift");
    Assert.True(v2 < v3, "v2 deve ser menor que v3");

    // ✅ RegistryVersion protege contra clock drift
    // ✅ Versões continuam monotonicamente crescentes
}
```

**Quando usar:** Testes unitários, testes de integração com tempo fixo, simulação de cenários temporais.

---

### 6️⃣ Conversão de/para long e DateTimeOffset

```csharp
// RegistryVersion → long (implícito)
RegistryVersion version = RegistryVersion.GenerateNewVersion();
long ticks = version;  // Conversão automática
SaveToDatabase(ticks);

// long → RegistryVersion (implícito)
long ticksFromDb = GetFromDatabase();
RegistryVersion convertedVersion = ticksFromDb;  // Conversão automática

// Explícito usando FromLong (mesmo resultado)
RegistryVersion explicitVersion = RegistryVersion.FromLong(ticksFromDb);

// RegistryVersion → DateTimeOffset
DateTimeOffset timestamp = version.AsDateTimeOffset;
Console.WriteLine($"Version timestamp: {timestamp:O}");
// Saída: Version timestamp: 2025-01-15T10:30:00.0000000+00:00

// DateTimeOffset → RegistryVersion
var dateTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
var versionFromDate = RegistryVersion.FromDateTimeOffset(dateTime);
```

**Quando usar:** Integração com código existente que usa `long` ou `DateTimeOffset`.

---

### 7️⃣ Comparação e Ordenação

```csharp
// Gerar várias versões
var versions = new List<RegistryVersion>();
for (int i = 0; i < 5; i++)
{
    versions.Add(RegistryVersion.GenerateNewVersion());
    Thread.Sleep(1);  // Pequena pausa
}

// Comparar versões
var first = versions[0];
var last = versions[4];

Assert.True(first < last);   // ✅ Primeiro é menor
Assert.True(last > first);   // ✅ Último é maior

// Ordenar lista de versões
versions.Reverse();  // Inverter ordem
var sorted = versions.OrderBy(v => v).ToList();

// sorted[0] == first ✅
// sorted[4] == last  ✅

// Usar como chave em dicionário
var eventDict = new Dictionary<RegistryVersion, OrderEvent>();
foreach (var version in versions)
{
    eventDict[version] = new OrderCreatedEvent { Sequence = version };
}

var @event = eventDict[first];  // ✅ Busca eficiente
```

**Quando usar:** Ordenação de eventos, busca por range, estruturas de dados.

---

## 📊 Impacto na Performance

### 💭 As Grandes Perguntas

#### **Pergunta 1: Por que RegistryVersion é tão rápido?**

> "RegistryVersion.GenerateNewVersion() leva ~25ns, quase o mesmo que DateTime.UtcNow.Ticks. Por quê?"

**Resposta:** Estrutura extremamente simples + ThreadStatic + zero alocações.

**Análise Detalhada:**

```csharp
// Internamente, RegistryVersion.GenerateNewVersion() faz:
public static RegistryVersion GenerateNewVersion()
{
    long ticks = DateTimeOffset.UtcNow.UtcTicks;  // ~24.5ns (baseline)

    if (ticks <= _lastTicks)  // Proteção clock drift (~0.5ns)
        ticks = _lastTicks + 1;

    _lastTicks = ticks;  // Armazenar (~0.1ns)
    return new RegistryVersion(ticks);  // Criar struct (~0.1ns)
}

// Total: ~25ns (essencialmente o custo de DateTime.UtcNow.Ticks)
```

**Por que tão rápido?**

1. **ThreadStatic**: Cada thread tem sua própria `_lastTicks`, zero contenção
2. **Sem randomness**: Não precisa gerar bytes aleatórios (Id precisa)
3. **Struct simples**: Apenas um `long`, sem estrutura complexa
4. **Operações mínimas**: Comparação + incremento condicional
5. **Zero alocações**: Tudo na stack, sem GC

**Comparação com alternativas:**

| Método | Custo | Análise |
|--------|-------|---------|
| **DateTime.UtcNow.Ticks** | ~24.5ns | Baseline nativo |
| **RegistryVersion.GenerateNewVersion()** | ~25ns | ✅ Essencialmente o mesmo custo! |
| **Id.GenerateNewId()** | ~73ns | ~3x mais lento (precisa gerar random bits) |
| **Guid.NewGuid()** | ~36ns | Mais rápido, mas sem ordenação temporal |

**Conclusão:** RegistryVersion é **essencialmente grátis** — o custo é apenas de ler o relógio do sistema.

---

#### **Pergunta 2: Quando usar RegistryVersion vs Id?**

> "Quando devo usar RegistryVersion ao invés de Id.GenerateNewId()?"

**Resposta:** Use **RegistryVersion para versioning/sequências**, use **Id para identificadores únicos distribuídos**.

**Matriz de Decisão:**

| Cenário | Recomendação | Razão |
|---------|--------------|-------|
| **Primary Key de Entidade** | ✅ **Id** | Unicidade global sem coordenação |
| **Versão para Optimistic Locking** | ✅ **RegistryVersion** | 3x mais rápido, 50% menos espaço |
| **Event Sourcing - Event ID** | ✅ **Id** | Unicidade global dos eventos |
| **Event Sourcing - Sequence Number** | ✅ **RegistryVersion** | Ordenação monotônica garantida |
| **Audit Log - Log ID** | ✅ **Id** | Unicidade global |
| **Audit Log - Version/Sequence** | ✅ **RegistryVersion** | Ordenação temporal |
| **API REST - Resource ID** | ✅ **Id** | Compatibilidade com UUID |
| **Distributed Systems - Node ID** | ✅ **Id** | Unicidade sem coordenação |
| **Single Instance - Sequential Number** | ✅ **RegistryVersion** | Mais rápido e compacto |

**Exemplo: Combinando ambos**

```csharp
// ✅ Use AMBOS juntos para máximo benefício!
public class Order
{
    public Id Id { get; private set; } = Id.GenerateNewId();  // ✨ Identidade única global
    public RegistryVersion Version { get; private set; } = RegistryVersion.GenerateNewVersion();  // ✨ Versão otimizada
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; private set; }

    public void UpdateTotal(decimal newTotal)
    {
        Total = newTotal;
        Version = RegistryVersion.GenerateNewVersion();  // ✨ Nova versão (~25ns)
    }
}

// Event Sourcing: Id para evento, RegistryVersion para sequência
public class OrderEvent
{
    public Id EventId { get; set; } = Id.GenerateNewId();  // ✨ Identidade única do evento
    public Guid AggregateId { get; set; }
    public RegistryVersion Sequence { get; set; } = RegistryVersion.GenerateNewVersion();  // ✨ Sequência monotônica
}
```

**Trade-offs:**

| Aspecto | RegistryVersion | Id |
|---------|-----------------|-----|
| **Performance** | ~25ns ✅ | ~73ns (3x mais lento) |
| **Tamanho** | 8 bytes ✅ | 16 bytes |
| **Único globalmente** | ❌ Não (requer coordenação) | ✅ Sim (46 bits random) |
| **Monotônico** | ✅ Por thread | ✅ Por thread |
| **Ideal para** | Versioning, sequences | Primary keys, distributed IDs |

---

### 📈 Resultados do Benchmark

Ambiente de teste:
- **Hardware:** AMD Ryzen 5 5600X (3.70GHz, 6 cores, 12 threads)
- **SO:** Windows 11 (10.0.26200.7171)
- **.NET:** 10.0.0 (10.0.0, 10.0.25.52411)
- **Modo:** Release com otimizações (x86-64-v3)
- **BenchmarkDotNet:** v0.15.6
- **Estratégia:** Throughput, WarmupCount=3, LaunchCount=1

---

#### 🏁 Tabela de Resultados

| Método | Mean | Error | StdDev | Ratio | Allocated |
|--------|------|-------|--------|-------|-----------|
| **DateTime.UtcNow.Ticks (Baseline)** | 24.56 ns | 0.11 ns | 0.10 ns | 1.00 | - |
| **RegistryVersion.GenerateNewVersion()** | 24.94 ns | 0.09 ns | 0.08 ns | 1.02 | - |
| **RegistryVersion.GenerateNewVersion(TimeProvider.System)** | 24.89 ns | 0.07 ns | 0.06 ns | 1.01 | - |
| **RegistryVersion.GenerateNewVersion(CustomTimeProvider)** | 26.34 ns | 0.14 ns | 0.13 ns | 1.07 | - |
| **RegistryVersion.GenerateNewVersion(DateTimeOffset)** | 24.88 ns | 0.05 ns | 0.04 ns | 1.01 | - |
| **RegistryVersion.GenerateNewVersion(FixedTimestamp)** | 0.27 ns | 0.02 ns | 0.02 ns | 0.01 | - |
| **RegistryVersion.FromLong(ticks)** | 24.91 ns | 0.49 ns | 0.49 ns | 1.01 | - |
| **RegistryVersion.FromDateTimeOffset()** | 24.68 ns | 0.05 ns | 0.04 ns | 1.00 | - |

---

#### 📊 Análise dos Resultados

**⚡ Performance Extrema:**

1. **RegistryVersion.GenerateNewVersion(): ~25ns**
   - Essencialmente o **mesmo custo** que `DateTime.UtcNow.Ticks` (~24.5ns)
   - **3x mais rápido** que `Id.GenerateNewId()` (~73ns)
   - **Zero alocações** no heap
   - Overhead de apenas **~0.4ns** para proteção de clock drift

2. **Por que tão rápido?**
   - ThreadStatic elimina locks e contenção
   - Estrutura simples (apenas um `long`)
   - Sem geração de bytes aleatórios
   - Operações mínimas (comparação + incremento)

3. **Comparação com Id.GenerateNewId():**
   ```
   Id.GenerateNewId():                ~73ns
   ├─ DateTime.UtcNow:                ~24.5ns
   ├─ RandomNumberGenerator.Fill():   ~35ns
   ├─ Construção de Guid:             ~10ns
   └─ Overhead ThreadStatic:          ~3.5ns

   RegistryVersion.GenerateNewVersion(): ~25ns
   ├─ DateTime.UtcNow:                ~24.5ns
   └─ Comparação + incremento:        ~0.5ns

   Diferença: ~48ns (RegistryVersion é 3x mais rápido!)
   ```

**🔬 Insights Importantes:**

1. **FixedTimestamp (0.27ns)**: Quando timestamp é passado como parâmetro, não há custo de `DateTime.UtcNow`
2. **CustomTimeProvider (+1.4ns)**: Overhead mínimo para injeção de dependência
3. **TimeProvider.System (+0.0ns)**: Sem overhead adicional vs geração padrão
4. **FromLong()/FromDateTimeOffset()**: Reconstrução de versões tem mesmo custo que geração (lê timestamp)

---

#### 🚀 Performance Por Operação Individual

| Método | Custo por Versão | Throughput | Análise |
|--------|------------------|------------|---------|
| **DateTime.UtcNow.Ticks** | ~24.5 ns | ~41M ticks/s | Baseline - leitura do relógio |
| **RegistryVersion.GenerateNewVersion()** | **~25 ns** | **~40M versions/s** | ✅ **Essencialmente grátis!** |
| **Id.GenerateNewId()** | ~73 ns | ~14M IDs/s | 3x mais lento (precisa random) |

---

### 📐 Metodologia de Benchmarks

#### **Como os Números Foram Obtidos**

**Fonte dos Dados:**
Todos os números de performance são derivados de **benchmarks reais** executados com BenchmarkDotNet v0.15.6.

**Benchmarks Executados:**

```csharp
[MemoryDiagnoser]
public class RegistryVersionBench
{
    [Benchmark(Baseline = true)]
    public long DateTimeUtcNowTicks()
    {
        return DateTime.UtcNow.Ticks;
    }

    [Benchmark]
    public RegistryVersion GenerateNewVersion()
    {
        return RegistryVersion.GenerateNewVersion();
    }

    [Benchmark]
    public RegistryVersion GenerateNewVersionWithTimeProvider()
    {
        return RegistryVersion.GenerateNewVersion(TimeProvider.System);
    }

    [Benchmark]
    public RegistryVersion GenerateNewVersionWithCustomTimeProvider()
    {
        return RegistryVersion.GenerateNewVersion(_customTimeProvider);
    }

    [Benchmark]
    public RegistryVersion GenerateNewVersionWithDateTimeOffset()
    {
        return RegistryVersion.GenerateNewVersion(DateTimeOffset.UtcNow);
    }

    [Benchmark]
    public RegistryVersion FromLong()
    {
        return RegistryVersion.FromLong(DateTime.UtcNow.Ticks);
    }

    [Benchmark]
    public RegistryVersion FromDateTimeOffset()
    {
        return RegistryVersion.FromDateTimeOffset(DateTimeOffset.UtcNow);
    }
}
```

**⚠️ Importante: Interpretar Corretamente**

- **Performance Isolada**: RegistryVersion.GenerateNewVersion() é ~25ns
  - Essencialmente o mesmo custo de ler o relógio do sistema
  - 3x mais rápido que Id.GenerateNewId() (~73ns)

- **Performance End-to-End**: RegistryVersion resulta em aplicações **mais rápidas**
  - Versões locais (sem round-trip ao banco)
  - 50% menos espaço em disco/memória
  - Optimistic locking mais eficiente

**Conclusão da Metodologia:**
Os ~25ns de RegistryVersion são **negligíveis** comparado ao benefício de eliminar round-trips ao banco (~1-5ms economizados por operação de update).

---

### 🔍 Análise Detalhada por Cenário

### 🎯 Cenário 1: Operação Individual

```
┌──────────────────────────────────────────────────────────────────────────┐
│           PERFORMANCE: OPERAÇÃO INDIVIDUAL                        │
└──────────────────────────────────────────────────────────────────────────┘
│                                                                   │
│ DateTime.UtcNow.Ticks (Baseline):          ~24.5 ns              │
│                                                                   │
│ 🚀 RegistryVersion.GenerateNewVersion():    ~25 ns ⚡            │
│    ✅ Overhead de apenas 0.5ns para proteção clock drift         │
│    ✅ Zero alocações no heap                                      │
│    ✅ 3x MAIS RÁPIDO que Id.GenerateNewId() (~73ns)             │
│    ✅ 50% MENOS espaço que Guid (8 bytes vs 16 bytes)           │
│                                                                   │
│ Por que tão rápido?                                              │
│    ✅ ThreadStatic: Zero contenção entre threads                 │
│    ✅ Sem randomness: Não precisa gerar bytes aleatórios         │
│    ✅ Struct simples: Apenas um long (8 bytes)                   │
│    ✅ Operações mínimas: Comparação + incremento                 │
│                                                                   │
└──────────────────────────────────────────────────────────────────────────┘
```

---

### 📦 Cenário 2: Optimistic Locking (End-to-End)

```
┌──────────────────────────────────────────────────────────────────────────┐
│           PERFORMANCE: OPTIMISTIC LOCKING END-TO-END              │
└──────────────────────────────────────────────────────────────────────────┘
│                                                                   │
│ Com versão gerada no banco:                                      │
│   Round-trip 1: Buscar entidade            ~2ms                  │
│   Round-trip 2: Atualizar                  ~2ms                  │
│   Round-trip 3: Buscar nova versão         ~2ms                  │
│   TOTAL:                                    ~6ms                  │
│                                                                   │
│ Com RegistryVersion:                                              │
│   Round-trip 1: Buscar entidade            ~2ms                  │
│   Gerar nova versão (local):               ~0.000025ms           │
│   Round-trip 2: Atualizar                  ~2ms                  │
│   TOTAL:                                    ~4ms ✅                │
│                                                                   │
│ Economia:                                    ~2ms (33% mais rápido!) │
│                                                                   │
│ Impacto em aplicação real (10.000 updates/seg):                 │
│   Versão do banco: 30.000 queries/seg                            │
│   RegistryVersion:  20.000 queries/seg ✅                         │
│   Economia:         10.000 queries/seg (33% menos carga!)        │
│                                                                   │
└──────────────────────────────────────────────────────────────────────────┘
```

---

### 🚀 Cenário 3: Event Sourcing (1M eventos)

```
┌──────────────────────────────────────────────────────────────────────────┐
│           PERFORMANCE: EVENT SOURCING (1M eventos)                │
└──────────────────────────────────────────────────────────────────────────┘
│                                                                   │
│ Gerar 1.000.000 de sequências:                                   │
│                                                                   │
│   RegistryVersion.GenerateNewVersion():                           │
│   1M × 25ns = 25.000.000 ns = 25ms                               │
│   Throughput: ~40 milhões de versões por segundo                 │
│                                                                   │
│   Id.GenerateNewId() (comparação):                                │
│   1M × 73ns = 73.000.000 ns = 73ms                               │
│   Throughput: ~14 milhões de IDs por segundo                     │
│                                                                   │
│ Economia: ~48ms para 1M eventos (3x mais rápido!)                │
│                                                                   │
│ Espaço em disco (1M eventos):                                    │
│   RegistryVersion: 8 MB (8 bytes × 1M)                           │
│   Guid:           16 MB (16 bytes × 1M)                          │
│   Economia:        8 MB (50% menos espaço!)                      │
│                                                                   │
└──────────────────────────────────────────────────────────────────────────┘
```

---

### 🔬 Interpretação Prática dos Números

#### Cenário Real: API REST com Optimistic Locking

```
Aplicação: API REST com 1,000 requests/segundo
Cada request: atualiza 1 entidade com optimistic locking
Total: 1,000 updates por segundo

Com versão gerada no banco:
  1,000 updates/s × 3 round-trips = 3,000 queries/seg
  Latência média por update: ~6ms
  Carga no banco: ~3,000 conexões concorrentes (pico)

Com RegistryVersion:
  1,000 updates/s × 2 round-trips = 2,000 queries/seg
  Latência média por update: ~4ms
  Carga no banco: ~2,000 conexões concorrentes (pico)

Benefícios:
  ✅ 33% menos queries no banco (1,000 queries/seg economizadas!)
  ✅ 33% menos latência (2ms economizados por update)
  ✅ 33% menos conexões concorrentes no banco
  ✅ Melhor utilização de recursos (CPU, memória, conexões)
  ✅ Overall performance improvement: ~33% em operações de escrita

💡 Conclusão: Eliminar 1 round-trip por update resulta em ganho
   MUITO maior que os ~25ns de geração da versão!
```

---

### 📋 Análise dos Resultados

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        CONCLUSÕES PRINCIPAIS                             │
└──────────────────────────────────────────────────────────────────────────┘
│                                                                           │
│ 1️⃣ PERFORMANCE BRUTA (Geração Isolada):                                 │
│      RegistryVersion.GenerateNewVersion() é ~25ns                       │
│      Essencialmente o MESMO custo que DateTime.UtcNow.Ticks            │
│      3x MAIS RÁPIDO que Id.GenerateNewId() (~73ns)                      │
│      Zero alocações no heap (struct + ThreadStatic)                     │
│      CONTEXTO: 25 nanosegundos = 0.000025 milissegundos                │
│       → Você geraria ~40 milhões de versões para "gastar" 1 segundo      │
│    ✅ BENEFÍCIO: Geração é ESSENCIALMENTE GRÁTIS                        │
│                                                                           │
│ 2️⃣ TAMANHO COMPACTO:                                                     │
│      8 bytes (50% menor que Guid/Id)                                    │
│      Economia de 8 bytes por campo de versão                            │
│      Em 1 milhão de registros: economia de ~8 MB                        │
│      CONTEXTO: Índices menores = melhor cache hit rate                  │
│    ✅ RECOMENDADO: Para campos de versão em todas as entidades!         │
│                                                                           │
│ 3️⃣ ORDENAÇÃO TEMPORAL (Benefício CRÍTICO):                               │
│      Versões ordenáveis por timestamp (UTC ticks)                       │
│      Monotonicidade garantida por thread                                │
│      Proteção contra clock drift integrada                              │
│      CONTEXTO: Ideal para event sourcing e audit logs                   │
│    ✅ RECOMENDADO: Para sequências monotônicas!                          │
│                                                                           │
│ 4️⃣ OPTIMISTIC LOCKING (Benefício ENORME):                                │
│      Elimina 1 round-trip ao banco (33% economia!)                      │
│      Latência ~2ms menor por update                                     │
│      Menos carga no banco de dados                                      │
│      CONTEXTO: Economia de 2ms >> custo de 25ns!                        │
│    🚀 RECOMENDADO: Use para TODAS as entidades com versão!               │
│                                                                           │
│ 5️⃣ CUSTO TOTAL (Aplicação End-to-End):                                   │
│      Geração: ~25ns (essencialmente grátis)                             │
│      Optimistic locking: ~33% mais rápido (1 round-trip a menos)        │
│      Armazenamento: 50% menos espaço que Guid                           │
│      RESULTADO: Improvement geral de 30-40% em operações de update      │
│    🚀 RECOMENDADO: Troque HOJE se você usa versões no banco!             │
│                                                                           │
│ 💭 DECISÃO FINAL:                                                         │
│      O custo de geração (~25ns) é NEGLIGÍVEL comparado ao benefício     │
│      de eliminar round-trips ao banco (~2ms economizados).              │
│      Em contexto real, RegistryVersion resulta em aplicações            │
│      significativamente MAIS RÁPIDAS e EFICIENTES.                      │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## ⚖️ Trade-offs

Nenhuma solução é perfeita. Aqui estão as vantagens e limitações do `RegistryVersion`:

### ✅ Vantagens

#### 1. **Performance Extrema**
- **~25 nanosegundos** por versão gerada
- **Essencialmente grátis** (mesmo custo que `DateTime.UtcNow.Ticks`)
- **3x mais rápido** que `Id.GenerateNewId()` (~25ns vs ~73ns)
- **Zero alocações** no heap (struct + ThreadStatic)
- **Zero contenção** entre threads (ThreadStatic)
- **Escala linearmente** com número de threads

```csharp
// Exemplo: Gerar 10M versões
for (int i = 0; i < 10_000_000; i++)
{
    var version = RegistryVersion.GenerateNewVersion();  // ~25ns cada
}
// Total: ~250ms para 10 milhões de versões!
```

#### 2. **Tamanho Compacto (50% menor que Guid)**
- **8 bytes** por versão (vs 16 bytes de Guid)
- **Economia significativa** em armazenamento
- **Índices menores** (melhor cache hit rate)
- **Menos I/O** em queries e updates

```csharp
// Economia de espaço:
// 1 milhão de registros com versão:
//   Guid:             16 MB
//   RegistryVersion:   8 MB
//   Economia:          8 MB (50%)!
```

#### 3. **Ordenação Temporal Precisa**
- Versões são **ordenáveis por timestamp** (UTC ticks)
- **Resolução de 100 nanosegundos** (10.000x melhor que milissegundos)
- **Monotonicidade garantida** por thread
- **Debugging facilitado** (ordem de modificações visível)

```csharp
var v1 = RegistryVersion.GenerateNewVersion();
Thread.Sleep(10);
var v2 = RegistryVersion.GenerateNewVersion();

Assert.True(v1 < v2);  // ✅ Ordenação garantida
```

#### 4. **Proteção Contra Clock Drift**
- Mantém **monotonicidade** mesmo se relógio retroceder
- Detecta e compensa ajustes de horário (NTP sync, virtualização)
- **Versões sempre crescentes** por thread

```csharp
// Mesmo com clock drift, nunca retrocede:
var v1 = RegistryVersion.GenerateNewVersion();  // ticks: 1000
// ⚠️ Relógio retrocede
var v2 = RegistryVersion.GenerateNewVersion();  // ticks: 1001 ✅ Ainda maior!
```

#### 5. **Compatibilidade Total com long**
- **Conversão implícita** para/de long
- Funciona com **Entity Framework Core** (armazena como `bigint`)
- **Tamanho idêntico** a long (8 bytes)
- Armazena diretamente no banco

```csharp
RegistryVersion version = RegistryVersion.GenerateNewVersion();
long ticks = version;  // ✅ Conversão automática

public void ProcessVersion(long versionTicks) { }
ProcessVersion(version);  // ✅ Funciona!
```

#### 6. **Optimistic Locking Eficiente**
- **Elimina 1 round-trip** ao banco (33% economia!)
- Versão gerada **localmente** antes de persistir
- **~2ms economizados** por operação de update
- Funciona perfeitamente com EF Core

```csharp
// ✅ Versão gerada localmente, sem round-trip extra
order.UpdateTotal(newTotal);
await _context.SaveChangesAsync();
// Versão já está atualizada localmente!
```

---

### ⚠️ Limitações

#### 1. **Monotonicidade é Por-Thread**

**Descrição:** Versões geradas na **mesma thread** são sequenciais, mas versões de **threads diferentes** podem intercalar.

```csharp
// Thread A:
var vA1 = RegistryVersion.GenerateNewVersion();  // ticks: 1000
var vA2 = RegistryVersion.GenerateNewVersion();  // ticks: 1001
// vA1 < vA2 ✅ (garantido na mesma thread)

// Thread B (executando simultaneamente):
var vB1 = RegistryVersion.GenerateNewVersion();  // ticks: 1000 ou 1001
var vB2 = RegistryVersion.GenerateNewVersion();  // ticks: 1001 ou 1002
// vB1 < vB2 ✅ (garantido na mesma thread)

// Mas a ordem GLOBAL entre threads pode variar:
// Possibilidade 1: vA1 < vA2 < vB1 < vB2
// Possibilidade 2: vB1 < vA1 < vB2 < vA2
// Depende do timestamp exato de execução de cada thread
```

**Quando importa:**
- Se você precisa de **ordem ESTRITA global** entre threads no mesmo milissegundo
- Exemplo: Sistema de filas onde ordem absoluta dentro do milissegundo é crítica

**Quando NÃO importa (maioria dos casos):**
- Optimistic locking (cada entidade tem sua própria thread de update)
- Event sourcing por aggregate (eventos de um aggregate são sequenciais)
- Audit logs (ordenação "próxima" do tempo real é suficiente)
- Diferença de ticks dentro do milissegundo é aceitável

**Solução (se necessário):**
```csharp
// Para ordem global estrita, use lock (mais lento):
public class StrictSequentialVersionGenerator
{
    private static readonly object _lock = new();

    public static RegistryVersion GenerateVersion()
    {
        lock (_lock)
        {
            return RegistryVersion.GenerateNewVersion();
        }
    }
}
// Custo: ~50-200ns por versão (ainda rápido, mas com contenção)
```

---

#### 2. **Não é Globalmente Único Sem Coordenação**

**Descrição:** Ao contrário de `Id` (UUIDv7), `RegistryVersion` **não garante unicidade global** entre diferentes instâncias/servidores sem coordenação.

```csharp
// Servidor A (timestamp: 1000):
var vA = RegistryVersion.GenerateNewVersion();  // ticks: 638401234567890000

// Servidor B (timestamp: 1000, MESMO milissegundo):
var vB = RegistryVersion.GenerateNewVersion();  // ticks: 638401234567890000

// ⚠️ vA == vB (MESMA versão!)
// Sem bits aleatórios para diferenciar instâncias
```

**Quando importa:**
- **Sistemas distribuídos** onde múltiplas instâncias geram versões simultaneamente
- **Sharding horizontal** sem coordenação central
- **Distributed primary keys** (use `Id` ao invés!)

**Quando NÃO importa:**
- **Single instance** (aplicação em uma única máquina)
- **Coordenação central** (todos os updates passam pelo mesmo servidor)
- **Versões por aggregate** (cada aggregate tem apenas uma instância escrevendo)
- **Versões são scoped** (cada entidade tem suas próprias versões)

**Comparação:**

| Cenário | RegistryVersion | Id (UUIDv7) |
|---------|-----------------|-------------|
| **Single instance** | ✅ Perfeito | ✅ Funciona (overhead desnecessário) |
| **Distributed sem coordenação** | ❌ Pode duplicar | ✅ Único globalmente |
| **Optimistic locking** | ✅ Ideal | ✅ Funciona (mais lento, maior) |
| **Event sourcing (single aggregate)** | ✅ Ideal | ✅ Funciona (overhead desnecessário) |

**Solução:**
- Use `RegistryVersion` para **versioning/sequences**
- Use `Id` para **identificadores únicos globais**
- **Combine ambos** quando precisar dos dois benefícios:

```csharp
public class Order
{
    public Id Id { get; private set; } = Id.GenerateNewId();  // ✨ Único globalmente
    public RegistryVersion Version { get; private set; } = RegistryVersion.GenerateNewVersion();  // ✨ Versão otimizada
}
```

---

#### 3. **Dependência do Relógio do Sistema**

**Descrição:** Usa `DateTimeOffset.UtcNow` para o timestamp embutido.

```csharp
// Internamente:
long ticks = DateTimeOffset.UtcNow.UtcTicks;
```

**Impactos:**

- **Ajustes grandes no relógio** podem afetar ordenação global
  - Exemplo: Admin ajusta relógio -1 hora
  - Versões geradas após ajuste terão timestamps antigos
  - Ordenação entre versões antes/depois do ajuste será incorreta

- **Sincronização NTP** geralmente é transparente
  - Ajustes pequenos (<1 segundo) são compensados pela proteção de clock drift
  - Proteção contra clock drift já implementada

**Mitigações:**
- ✅ Proteção contra clock drift (já implementada)
- ✅ Monotonicidade por thread mantida (mesmo com ajuste)
- ⚠️ Ordenação global pode ser afetada por ajustes grandes (>1 segundo)

**Quando importa:**
- Ambientes com ajustes manuais frequentes de relógio
- Virtualização com time drift alto (VMs antigas)

**Quando NÃO importa:**
- Servidores modernos com NTP configurado
- Cloud providers (AWS, Azure, GCP) com time sync automático
- 99.9% dos casos em produção

---

### 💭 Resumo: Devo Usar RegistryVersion?

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     DECISÃO: USAR RegistryVersion?                       │
└──────────────────────────────────────────────────────────────────────────┘
│                                                                           │
│ ✅ USE RegistryVersion SE:                                                │
│    ✓ Você precisa de versões para optimistic locking                    │
│    ✓ Você quer performance máxima (~25ns, 3x mais rápido que Id)        │
│    ✓ Você quer economizar espaço (8 bytes vs 16 bytes de Guid)          │
│    ✓ Você usa event sourcing (sequence numbers monotônicos)             │
│    ✓ Você precisa de audit logs com ordenação temporal                  │
│    ✓ Você está em single instance ou com coordenação central            │
│    ✓ Você usa Entity Framework Core (conversão automática para long)    │
│    ✓ Você quer eliminar round-trips ao banco (33% mais rápido!)         │
│                                                                           │
│ ⚠️ CONSIDERE ALTERNATIVAS SE:                                             │
│    ✓ Você precisa de unicidade global sem coordenação                   │
│       → Solução: Use Id (UUIDv7) para identificadores                   │
│    ✓ Você precisa de ordem ESTRITA global entre threads                 │
│       → Solução: Use lock wrapper (ainda rápido, ~50-200ns)             │
│                                                                           │
│ ❌ NÃO USE RegistryVersion SE:                                            │
│    ✓ Você precisa de primary keys distribuídos                          │
│       → Use: Id (UUIDv7) para unicidade global                          │
│    ✓ Você precisa de versões únicas entre múltiplas instâncias          │
│       → Use: Id (UUIDv7) ou coordenação central                         │
│                                                                           │
│ 💭 RECOMENDAÇÃO GERAL:                                                    │
│    USE RegistryVersion para VERSÕES de entidades! ✅                     │
│    USE Id para IDENTIFICADORES de entidades! ✅                          │
│    USE AMBOS JUNTOS para máximo benefício! 🚀                            │
│                                                                           │
│ 🔥 Pattern Recomendado:                                                   │
│    public class Order                                                    │
│    {                                                                     │
│        public Id Id { get; set; }              // Identidade global     │
│        public RegistryVersion Version { get; set; }  // Versão otimizada│
│    }                                                                     │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 🧪 Exemplos Avançados

### Exemplo 1: Optimistic Locking com Entity Framework Core

```csharp
// Entidade
public class Product
{
    public Id Id { get; private set; } = Id.GenerateNewId();
    public RegistryVersion Version { get; private set; } = RegistryVersion.GenerateNewVersion();
    public string Name { get; set; }
    public decimal Price { get; private set; }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0)
            throw new ArgumentException("Price cannot be negative");

        Price = newPrice;
        Version = RegistryVersion.GenerateNewVersion();  // ✨ Nova versão
    }
}

// DbContext
public class AppDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Configurar Id
            entity.Property(e => e.Id)
                .HasConversion(
                    id => id.Value,
                    guid => Id.FromGuid(guid)
                );

            // Configurar RegistryVersion com optimistic locking
            entity.Property(e => e.Version)
                .HasConversion(
                    version => version.Value,              // RegistryVersion → long
                    ticks => RegistryVersion.FromLong(ticks)    // long → RegistryVersion
                )
                .IsConcurrencyToken();  // ✨ EF Core gerencia concorrência automaticamente!

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Price)
                .HasPrecision(18, 2);
        });
    }
}

// Service com optimistic locking
public class ProductService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(AppDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductDto> UpdateProductPriceAsync(Guid productId, decimal newPrice)
    {
        var product = await _context.Products.FindAsync(productId);

        if (product == null)
            throw new NotFoundException($"Product {productId} not found");

        var oldVersion = product.Version;

        // ✨ Atualiza preço e gera nova versão LOCALMENTE
        product.UpdatePrice(newPrice);

        _logger.LogInformation(
            "Updating product {ProductId} price from {OldPrice} to {NewPrice}, version {OldVersion} → {NewVersion}",
            productId, product.Price, newPrice, oldVersion, product.Version
        );

        try
        {
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Product {ProductId} updated successfully to version {NewVersion}",
                productId, product.Version
            );

            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                Version = product.Version
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                "Concurrency conflict updating product {ProductId} at version {Version}",
                productId, oldVersion
            );

            throw new ConcurrencyException(
                $"Product {productId} was modified by another user. Please refresh and try again.",
                ex
            );
        }
    }
}

// DTO
public class ProductDto
{
    public Id Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public RegistryVersion Version { get; set; }
}

// Exception customizada
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

---

### Exemplo 2: Event Sourcing Completo

```csharp
// Evento base
public abstract class DomainEvent
{
    public Id EventId { get; set; } = Id.GenerateNewId();
    public Guid AggregateId { get; set; }
    public RegistryVersion Sequence { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
}

// Eventos específicos
public class OrderCreatedEvent : DomainEvent
{
    public Guid CustomerId { get; set; }
    public decimal Total { get; set; }
}

public class OrderItemAddedEvent : DomainEvent
{
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderPaidEvent : DomainEvent
{
    public string PaymentMethod { get; set; }
    public decimal AmountPaid { get; set; }
}

// Aggregate
public class OrderAggregate
{
    private readonly List<DomainEvent> _uncommittedEvents = new();

    public Guid Id { get; private set; }
    public RegistryVersion CurrentSequence { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Total { get; private set; }
    public bool IsPaid { get; private set; }

    // Factory method
    public static OrderAggregate Create(Guid customerId, decimal initialTotal)
    {
        var aggregate = new OrderAggregate { Id = Guid.NewGuid() };

        aggregate.RaiseEvent(new OrderCreatedEvent
        {
            AggregateId = aggregate.Id,
            Sequence = RegistryVersion.GenerateNewVersion(),  // ✨ Primeira sequência
            CustomerId = customerId,
            Total = initialTotal
        });

        return aggregate;
    }

    // Commands
    public void AddItem(string productName, int quantity, decimal unitPrice)
    {
        RaiseEvent(new OrderItemAddedEvent
        {
            AggregateId = Id,
            Sequence = RegistryVersion.GenerateNewVersion(),  // ✨ Sequência automática
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice
        });
    }

    public void Pay(string paymentMethod, decimal amount)
    {
        if (IsPaid)
            throw new InvalidOperationException("Order already paid");

        if (amount < Total)
            throw new InvalidOperationException("Insufficient payment amount");

        RaiseEvent(new OrderPaidEvent
        {
            AggregateId = Id,
            Sequence = RegistryVersion.GenerateNewVersion(),  // ✨ Sequência monotônica
            PaymentMethod = paymentMethod,
            AmountPaid = amount
        });
    }

    // Event application
    private void RaiseEvent(DomainEvent @event)
    {
        Apply(@event);
        CurrentSequence = @event.Sequence;
        _uncommittedEvents.Add(@event);
    }

    private void Apply(DomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreatedEvent created:
                CustomerId = created.CustomerId;
                Total = created.Total;
                break;

            case OrderItemAddedEvent itemAdded:
                Total += itemAdded.Quantity * itemAdded.UnitPrice;
                break;

            case OrderPaidEvent paid:
                IsPaid = true;
                break;
        }
    }

    // Replay from events
    public static OrderAggregate LoadFromHistory(IEnumerable<DomainEvent> history)
    {
        var aggregate = new OrderAggregate();

        foreach (var @event in history.OrderBy(e => e.Sequence))  // ✨ Ordenação garantida!
        {
            aggregate.Apply(@event);
            aggregate.CurrentSequence = @event.Sequence;
        }

        return aggregate;
    }

    public IReadOnlyList<DomainEvent> GetUncommittedEvents() => _uncommittedEvents;
    public void MarkEventsAsCommitted() => _uncommittedEvents.Clear();
}

// Repository
public class OrderRepository
{
    private readonly IEventStore _eventStore;

    public OrderRepository(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<OrderAggregate> GetByIdAsync(Guid orderId)
    {
        var events = await _eventStore.GetEventsAsync(orderId);

        if (!events.Any())
            throw new NotFoundException($"Order {orderId} not found");

        return OrderAggregate.LoadFromHistory(events);
    }

    public async Task SaveAsync(OrderAggregate aggregate)
    {
        var events = aggregate.GetUncommittedEvents();

        if (events.Any())
        {
            await _eventStore.SaveEventsAsync(aggregate.Id, events, aggregate.CurrentSequence);
            aggregate.MarkEventsAsCommitted();
        }
    }
}

// Event Store (interface)
public interface IEventStore
{
    Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId);
    Task SaveEventsAsync(Guid aggregateId, IReadOnlyList<DomainEvent> events, RegistryVersion expectedVersion);
}

// Uso
public class OrderService
{
    private readonly OrderRepository _repository;

    public async Task<Guid> CreateOrderAsync(Guid customerId, decimal initialTotal)
    {
        var order = OrderAggregate.Create(customerId, initialTotal);
        await _repository.SaveAsync(order);
        return order.Id;
    }

    public async Task AddItemToOrderAsync(Guid orderId, string productName, int quantity, decimal unitPrice)
    {
        var order = await _repository.GetByIdAsync(orderId);
        order.AddItem(productName, quantity, unitPrice);
        await _repository.SaveAsync(order);
    }

    public async Task PayOrderAsync(Guid orderId, string paymentMethod, decimal amount)
    {
        var order = await _repository.GetByIdAsync(orderId);
        order.Pay(paymentMethod, amount);
        await _repository.SaveAsync(order);
    }
}
```

---

### Exemplo 3: Audit Trail Completo

```csharp
// Audit entry
public class AuditEntry
{
    public Id Id { get; set; } = Id.GenerateNewId();
    public Guid EntityId { get; set; }
    public string EntityType { get; set; }
    public RegistryVersion Version { get; set; } = RegistryVersion.GenerateNewVersion();
    public string Action { get; set; }  // Created, Updated, Deleted
    public string UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Changes { get; set; }  // JSON with old/new values
}

// Auditable entity base
public abstract class AuditableEntity
{
    public Id Id { get; protected set; } = Id.GenerateNewId();
    public RegistryVersion Version { get; protected set; } = RegistryVersion.GenerateNewVersion();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public string CreatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string UpdatedBy { get; protected set; }

    protected void UpdateVersion(string userId)
    {
        Version = RegistryVersion.GenerateNewVersion();
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = userId;
    }
}

// Example entity
public class Customer : AuditableEntity
{
    public string Name { get; private set; }
    public string Email { get; private set; }

    public void UpdateEmail(string newEmail, string userId)
    {
        Email = newEmail;
        UpdateVersion(userId);
    }
}

// Audit service
public class AuditService
{
    private readonly AppDbContext _context;

    public async Task<IEnumerable<AuditEntry>> GetAuditTrailAsync(Guid entityId)
    {
        return await _context.AuditEntries
            .Where(a => a.EntityId == entityId)
            .OrderBy(a => a.Version)  // ✨ Ordenação garantida por versão!
            .ToListAsync();
    }

    public async Task<AuditEntry> RecordChangeAsync(
        Guid entityId,
        string entityType,
        string action,
        string userId,
        object changes)
    {
        var entry = new AuditEntry
        {
            EntityId = entityId,
            EntityType = entityType,
            Action = action,
            UserId = userId,
            Changes = JsonSerializer.Serialize(changes)
        };

        _context.AuditEntries.Add(entry);
        await _context.SaveChangesAsync();

        return entry;
    }
}
```

---

## 📚 Referências

### Documentação Relacionada

- [Id (UUIDv7) - Identificadores Únicos Distribuídos](../ids/id.md)
- [TimeProvider - Abstração de Tempo para Testabilidade](../../core/time-providers/time-provider.md)

### Conceitos Relacionados

- **Optimistic Locking**: Controle de concorrência sem locks
- **Event Sourcing**: Armazenamento de eventos ao invés de estado
- **CQRS**: Command Query Responsibility Segregation
- **UTC Ticks**: Intervalos de 100 nanosegundos desde 01/01/0001

### Quando Usar Cada Um

| Necessidade | Solução Recomendada |
|-------------|---------------------|
| **Primary Key distribuído** | ✅ `Id.GenerateNewId()` |
| **Versão para optimistic locking** | ✅ `RegistryVersion.GenerateNewVersion()` |
| **Event ID único global** | ✅ `Id.GenerateNewId()` |
| **Event sequence number** | ✅ `RegistryVersion.GenerateNewVersion()` |
| **Audit log ID** | ✅ `Id.GenerateNewId()` |
| **Audit log version** | ✅ `RegistryVersion.GenerateNewVersion()` |
| **API REST resource ID** | ✅ `Id.GenerateNewId()` |
| **Change tracking version** | ✅ `RegistryVersion.GenerateNewVersion()` |

### Performance Summary

```
┌──────────────────────────────────────────────────────────────┐
│                 PERFORMANCE COMPARISON                       │
├──────────────────────────────────────────────────────────────┤
│ DateTime.UtcNow.Ticks:           ~24.5 ns (baseline)         │
│ RegistryVersion.GenerateNewVersion(): ~25 ns (essencialmente grátis!) │
│ Id.GenerateNewId():                  ~73 ns (3x mais lento)   │
│ Guid.NewGuid():                      ~36 ns (sem ordenação)   │
├──────────────────────────────────────────────────────────────┤
│ RECOMENDAÇÃO:                                                │
│ - Use RegistryVersion para VERSÕES (optimistic locking)     │
│ - Use Id para IDENTIFICADORES (primary keys distribuídos)   │
│ - Combine ambos para máximo benefício! 🚀                    │
└──────────────────────────────────────────────────────────────┘
```

---

**💡 Dica Final:** RegistryVersion é perfeito para **versionamento de entidades** onde você precisa de números monotônicos ultrarrápidos (~25ns) e compactos (8 bytes), mas não precisa de unicidade global sem coordenação. Para identificadores únicos distribuídos, use `Id` (UUIDv7). Combine ambos para obter o melhor dos dois mundos!
