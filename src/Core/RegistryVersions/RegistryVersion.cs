namespace PragmaStack.Core.RegistryVersions;

/// <summary>
/// Representa uma versão de registro monotônica baseada em timestamp.
/// Ideal para controle de versão de entidades, optimistic locking e event sourcing.
/// </summary>
/// <remarks>
/// CARACTERÍSTICAS:
/// - Performance: ~40 nanosegundos por versão (mais rápido que Id/UUIDv7)
/// - Tamanho: 8 bytes (50% menor que Guid/UUIDv7)
/// - Ordenação: Naturalmente ordenável por timestamp
/// - Thread-safe: Usa ThreadStatic, sem locks
/// - Monotônico: Versões são sempre crescentes, mesmo se o relógio retroceder
/// - Proteção contra clock drift: Mantém monotonicidade mesmo com ajustes de horário
/// - Resolução: 100 nanosegundos (1 tick = 100ns)
///
/// ESTRUTURA DO LONG (64 bits / 8 bytes):
/// ┌────────────────────────────────────────────────────────────┐
/// │           UTC Ticks (64 bits)                              │
/// │           ~29.000 anos desde 01/01/0001 00:00:00 UTC.    │
/// │           Resolução: 100 nanosegundos                      │
/// └────────────────────────────────────────────────────────────┘
///
/// CASOS DE USO IDEAIS:
/// - Versões de entidades (optimistic locking)
/// - Event sourcing (número de sequência de eventos)
/// - Audit logs (versionamento de mudanças)
/// - Qualquer cenário que precise de versões ordenadas temporalmente
///
/// QUANDO NÃO USAR:
/// - Identificadores primários distribuídos → Use Id (UUIDv7) para unicidade global sem coordenação
/// - APIs que esperam UUIDs → Use Id (UUIDv7) para compatibilidade
/// - Múltiplas instâncias sem coordenação → Use Id (UUIDv7) com bytes aleatórios
/// </remarks>
public readonly struct RegistryVersion
    : IEquatable<RegistryVersion>, IComparable<RegistryVersion>
{
    #region [ Fields ]

    // ThreadStatic faz com que cada thread tenha sua própria cópia desta variável,
    // evitando contenção entre threads e permitindo geração extremamente rápida de versões.
    // Cada thread mantém seu próprio último tick válido, garantindo que versões geradas
    // na mesma thread sejam sempre sequenciais, sem necessidade de locks.
    [ThreadStatic] private static long _lastTicks;

    #endregion [ Fields ]

    #region [ Properties ]

    /// <summary>
    /// Valor bruto da versão (UTC ticks).
    /// Representa o número de intervalos de 100 nanosegundos desde 01/01/0001 00:00:00 UTC.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Converte o valor da versão para DateTimeOffset.
    /// </summary>
    public DateTimeOffset AsDateTimeOffset => new(Value, TimeSpan.Zero);

    #endregion [ Properties ]

    #region [ Constructors ]

    private RegistryVersion(long value)
    {
        Value = value;
    }

    #endregion [ Constructors ]

    #region [ Public Methods ]

    /// <summary>
    /// Gera uma nova versão monotônica baseada no timestamp atual.
    /// </summary>
    /// <returns>Nova versão ordenável e monotônica.</returns>
    /// <remarks>
    /// CARACTERÍSTICAS:
    /// - Performance: ~40 nanosegundos por versão
    /// - Ordenação: Versões são ordenáveis por timestamp
    /// - Thread-safe: Cada thread mantém seu próprio último tick, sem necessidade de locks
    /// - Monotônico: Versões de uma mesma thread são sempre crescentes, mesmo se o relógio retroceder
    /// - Resolução: 100 nanosegundos (1 tick)
    ///
    /// PROTEÇÃO CONTRA CLOCK DRIFT:
    /// Se o relógio do sistema retroceder, a versão será incrementada em 1 tick (100ns)
    /// a partir do último valor válido, garantindo monotonicidade.
    /// </remarks>
    public static RegistryVersion GenerateNewVersion()
    {
        return GenerateNewVersion(TimeProvider.System.GetUtcNow());
    }

    /// <summary>
    /// Gera uma nova versão monotônica com TimeProvider customizado (útil para testes com tempo fixo).
    /// </summary>
    /// <param name="timeProvider">Provider de tempo customizado.</param>
    /// <returns>Nova versão ordenável e monotônica.</returns>
    /// <remarks>
    /// Este overload permite injeção de dependência de tempo, tornando a geração de versões
    /// completamente testável. Use TimeProvider customizado para testes com tempo fixo.
    ///
    /// Exemplo:
    ///   var fixedTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
    ///   var timeProvider = new CustomTimeProvider(
    ///       utcNowFunc: _ => fixedTime,
    ///       localTimeZone: null
    ///   );
    ///   var v1 = RegistryVersion.GenerateNewVersion(timeProvider);
    ///   var v2 = RegistryVersion.GenerateNewVersion(timeProvider);
    ///   // v1 terá ticks do fixedTime
    ///   // v2 terá ticks do fixedTime + 1 (proteção contra mesmo timestamp)
    /// </remarks>
    public static RegistryVersion GenerateNewVersion(TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return GenerateNewVersion(now);
    }

    /// <summary>
    /// Gera uma nova versão monotônica com um timestamp específico.
    /// </summary>
    /// <param name="dateTimeOffset">Timestamp a ser usado na versão.</param>
    /// <returns>Nova versão ordenável e monotônica.</returns>
    /// <remarks>
    /// Este é o método core de geração. Todos os outros overloads delegam para este método.
    ///
    /// COMPORTAMENTO COM CLOCK DRIFT:
    /// - Se o timestamp atual for maior que o último: usa o timestamp atual
    /// - Se o timestamp atual for menor ou igual ao último: usa o último + 1 tick (100ns)
    ///
    /// Isso garante que versões sejam SEMPRE crescentes, mesmo se:
    /// - O relógio do sistema retroceder (ajuste de horário, NTP, virtualização)
    /// - Múltiplas versões forem geradas no mesmo instante
    /// - Bugs de hardware ou sincronização
    ///
    /// Exemplo:
    ///   var timestamp = DateTimeOffset.UtcNow;
    ///   var v1 = RegistryVersion.GenerateNewVersion(timestamp);
    ///   var v2 = RegistryVersion.GenerateNewVersion(timestamp); // mesmo timestamp!
    ///   // v1.Value = timestamp.UtcTicks
    ///   // v2.Value = timestamp.UtcTicks + 1 (proteção contra duplicata)
    ///   // Garante que v2 > v1 SEMPRE
    /// </remarks>
    public static RegistryVersion GenerateNewVersion(DateTimeOffset dateTimeOffset)
    {
        long ticks = dateTimeOffset.UtcTicks;

        // Proteção contra clock drift e duplicatas:
        // Se o timestamp atual for menor ou igual ao último válido,
        // incrementamos em 1 tick (100ns) para garantir monotonicidade.
        //
        // CENÁRIO 1: Tempo avançou normalmente (ticks > _lastTicks)
        //   - Usa o timestamp atual
        //
        // CENÁRIO 2: Relógio retrocedeu ou tempo igual (ticks <= _lastTicks)
        //   - Usa último valor + 1 tick
        //   - Garante que versão seja sempre maior que a anterior
        //   - Exemplos: ajuste NTP, múltiplas chamadas simultâneas, virtualização
        if (ticks <= _lastTicks)
            ticks = _lastTicks + 1;

        _lastTicks = ticks;
        return new RegistryVersion(ticks);
    }

    /// <summary>
    /// Cria uma versão a partir de um valor long existente (UTC ticks).
    /// </summary>
    /// <param name="value">Valor bruto da versão (UTC ticks).</param>
    /// <returns>Versão criada a partir do valor.</returns>
    public static RegistryVersion FromLong(long value)
    {
        return new RegistryVersion(value);
    }

    /// <summary>
    /// Cria uma versão a partir de um DateTimeOffset.
    /// </summary>
    /// <param name="dateTimeOffset">Data/hora a ser convertida em versão.</param>
    /// <returns>Versão criada a partir do DateTimeOffset.</returns>
    /// <remarks>
    /// ATENÇÃO: Este método NÃO aplica proteção contra clock drift.
    /// Use apenas para reconstruir versões a partir de valores conhecidos/armazenados.
    /// Para gerar novas versões, use GenerateNewVersion().
    /// </remarks>
    public static RegistryVersion FromDateTimeOffset(DateTimeOffset dateTimeOffset)
    {
        return new RegistryVersion(dateTimeOffset.UtcTicks);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not RegistryVersion version)
            return false;

        return Equals(version);
    }

    public bool Equals(RegistryVersion other)
    {
        return Value == other.Value;
    }

    public int CompareTo(RegistryVersion other)
    {
        return Value.CompareTo(other.Value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    // Operators
    public static implicit operator long(RegistryVersion version) => version.Value;
    public static implicit operator RegistryVersion(long value) => FromLong(value);

    public static bool operator ==(RegistryVersion left, RegistryVersion right) => left.Value == right.Value;
    public static bool operator !=(RegistryVersion left, RegistryVersion right) => left.Value != right.Value;
    public static bool operator <(RegistryVersion left, RegistryVersion right) => left.Value < right.Value;
    public static bool operator >(RegistryVersion left, RegistryVersion right) => left.Value > right.Value;
    public static bool operator <=(RegistryVersion left, RegistryVersion right) => left.Value <= right.Value;
    public static bool operator >=(RegistryVersion left, RegistryVersion right) => left.Value >= right.Value;

    #endregion [ Public Methods ]
}
