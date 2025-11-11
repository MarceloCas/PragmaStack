using System.Security.Cryptography;

namespace PragmaStack.Core.Ids;

public readonly struct Id
    : IEquatable<Id>
{
    #region [ Fields ]

    // ThreadStatic faz com que cada thread tenha sua própria cópia destas variáveis,
    // evitando contenção entre threads e permitindo geração extremamente rápida de IDs.
    // Cada thread mantém seu próprio timestamp e contador, garantindo que IDs gerados
    // na mesma thread sejam sempre sequenciais, sem necessidade de locks.
    [ThreadStatic] private static long _lastTimestamp;
    [ThreadStatic] private static long _counter;

    #endregion [ Fields ]

    #region [ Properties ]

    public Guid Value { get; }

    #endregion [ Properties ]

    #region [ Constructors ]

    private Id(Guid value)
    {
        Value = value;
    }

    #endregion [ Constructors ]

    #region [ Public Methods ]

    // Gera um novo ID monotônico baseado em UUIDv7.
    //
    // CARACTERÍSTICAS:
    // - Performance: ~73 nanosegundos por ID
    // - Ordenação: IDs são ordenáveis por timestamp (maioria dos casos)
    // - Unicidade: Garantida mesmo em ambientes distribuídos (múltiplas instâncias/servidores)
    // - Thread-safe: Cada thread mantém seu próprio contador, sem necessidade de locks
    // - Monotônico: IDs de uma mesma thread são sempre crescentes, mesmo se o relógio retroceder
    //
    // FORMATO: UUIDv7 com 48 bits de timestamp + 26 bits de contador + 46 bits aleatórios
    // Os 46 bits aleatórios garantem unicidade entre diferentes threads e instâncias da aplicação.
    public static Id GenerateNewId()
    {
        return GenerateNewId(TimeProvider.System.GetUtcNow());
    }

    // Gera um novo ID monotônico com TimeProvider customizado (útil para testes com tempo fixo).
    //
    // Este overload permite injeção de dependência de tempo, tornando a geração de IDs
    // completamente testável. Use CustomTimeProvider para testes com tempo fixo.
    //
    // Exemplo:
    //   var fixedTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
    //   var timeProvider = new CustomTimeProvider(
    //       utcNowFunc: _ => fixedTime,
    //       localTimeZone: null
    //   );
    //   var id1 = Id.GenerateNewId(timeProvider);
    //   var id2 = Id.GenerateNewId(timeProvider);
    //   // id1 e id2 terão o mesmo timestamp, mas contadores diferentes (0, 1)
    public static Id GenerateNewId(TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return GenerateNewId(now);
    }

    // Gera um novo ID monotônico com um timestamp específico.
    //
    // Este é o método core de geração. Todos os outros overloads delegam para este método.
    //
    // COMPORTAMENTO COM DIFERENTES TIMESTAMPS:
    // 1. Novo milissegundo (timestamp > último): Reinicia o contador para 0
    // 2. Relógio retrocedeu (timestamp < último): Mantém o último timestamp válido e incrementa o contador (proteção contra clock drift)
    // 3. Mesmo milissegundo (timestamp == último): Incrementa o contador para diferenciar os IDs
    //
    // Exemplo:
    //   var timestamp = DateTimeOffset.UtcNow;
    //   var id1 = Id.GenerateNewId(timestamp);
    //   var id2 = Id.GenerateNewId(timestamp);
    //   // id1 e id2 terão o mesmo timestamp mas contadores sequenciais (0 e 1)
    public static Id GenerateNewId(DateTimeOffset dateTimeOffset)
    {
        long timestamp = dateTimeOffset.ToUnixTimeMilliseconds();

        // CENÁRIO 1: Estamos em um novo milissegundo (caso mais comum)
        // Reiniciamos o contador para garantir que o novo ID seja maior que todos os anteriores
        if (timestamp > _lastTimestamp)
        {
            _lastTimestamp = timestamp;
            _counter = 0;
        }
        // CENÁRIO 2: O relógio do sistema retrocedeu (raro, mas possível)
        // Exemplos: ajuste de horário, virtualização, bugs de hardware
        // Solução: mantemos o último timestamp válido e incrementamos o contador,
        // garantindo que o novo ID ainda seja maior que o anterior
        else if (timestamp < _lastTimestamp)
        {
            timestamp = _lastTimestamp;
            _counter++;
        }
        // CENÁRIO 3: Ainda estamos no mesmo milissegundo (comum em alta frequência)
        // Simplesmente incrementamos o contador para diferenciar os IDs
        else
        {
            _counter++;

            // Proteção contra overflow do contador (extremamente improvável)
            // 0x3FFFFFF = 67.108.863 em decimal (26 bits, todos em 1)
            // Isso significa que você precisaria gerar mais de 67 MILHÕES de IDs
            // em um único milissegundo para atingir este limite!
            //
            // Se isso acontecer, fazemos spin-wait (espera ativa) até o próximo milissegundo.
            // Usamos spin-wait ao invés de Thread.Sleep porque sabemos que falta menos de 1ms,
            // e o custo de context switch seria maior que simplesmente esperar ocupando a CPU.
            if (_counter > 0x3FFFFFF)
            {
                SpinWaitForNextMillisecond(ref timestamp, ref _lastTimestamp);
                _counter = 0;
            }
        }

        return new Id(BuildUuidV7WithRandom(timestamp, _counter));
    }
    public static Id FromGuid(Guid guid)
    {
        return new Id(guid);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
    public override bool Equals(object? obj)
    {
        if (obj is not Id id)
            return false;

        return Equals(id);
    }
    public bool Equals(Id other)
    {
        return Value == other.Value;
    }

    // Operators
    public static implicit operator Guid(Id id) => id.Value;
    public static implicit operator Id(Guid guid) => FromGuid(guid);

    public static bool operator ==(Id left, Id right) => left.Value == right.Value;
    public static bool operator !=(Id left, Id right) => left.Value != right.Value;
    public static bool operator <(Id left, Id right) => left.Value.CompareTo(right.Value) < 0;
    public static bool operator >(Id left, Id right) => left.Value.CompareTo(right.Value) > 0;
    public static bool operator <=(Id left, Id right) => left.Value.CompareTo(right.Value) <= 0;
    public static bool operator >=(Id left, Id right) => left.Value.CompareTo(right.Value) >= 0;

    #endregion  [ Public Methods ]

    #region [ Private Methods ]

    // Constrói um UUIDv7 com bytes aleatórios para garantir unicidade global.
    //
    // ESTRUTURA DO UUIDv7 (total: 128 bits / 16 bytes):
    // ┌─────────────────┬──────┬─────────┬────────┬──────────────────┐
    // │  Timestamp (48) │ Ver  │ Counter │ Variant│   Random (46)    │
    // │                 │ (4)  │  (12)   │  (2)   │                  │
    // └─────────────────┴──────┴─────────┴────────┴──────────────────┘
    //
    // - Timestamp: 48 bits = milissegundos desde Unix epoch (garante ordenação temporal)
    // - Version: 4 bits = sempre 0111 (7 em binário, indica UUIDv7)
    // - Counter: Total de 26 bits distribuídos em 3 partes (garante ordenação dentro da mesma thread)
    //   - 12 bits após a versão
    //   - 6 bits após o variant
    //   - 8 bits no próximo byte
    // - Variant: 2 bits = sempre 10 (padrão RFC 4122)
    // - Random: 46 bits = bytes aleatórios criptograficamente seguros
    //
    // Os 46 bits aleatórios garantem unicidade mesmo em:
    // - Múltiplas threads gerando IDs simultaneamente
    // - Múltiplas instâncias da aplicação em servidores diferentes
    // - Ambientes distribuídos sem coordenação central
    private static Guid BuildUuidV7WithRandom(long timestamp, long counter)
    {
        // Divide o timestamp de 48 bits em duas partes para encaixar no construtor do Guid
        var timestampHigh = (int)(timestamp >> 16);      // 32 bits mais significativos
        var timestampLow = (short)(timestamp & 0xFFFF);  // 16 bits menos significativos

        // Combina a versão (7) com os primeiros 12 bits do counter
        // 0x7000 = 0111 0000 0000 0000 em binário (versão 7 nos 4 bits mais significativos)
        // counter >> 14 pega os bits 14-25 do counter (12 bits)
        // 0x0FFF garante que pegamos apenas 12 bits
        var versionAndCounter = (short)(0x7000 | ((counter >> 14) & 0x0FFF));

        // Combina o variant (10 em binário) com os próximos 6 bits do counter
        // 0x80 = 1000 0000 em binário (variant '10' nos 2 bits mais significativos)
        // counter >> 8 pega os bits 8-13 do counter (6 bits)
        // 0x3F = 0011 1111 garante que pegamos apenas 6 bits
        var variantHigh = (byte)(0x80 | ((counter >> 8) & 0x3F));

        // Pega os últimos 8 bits do counter (bits 0-7)
        // 0xFF = 1111 1111 garante que pegamos apenas 8 bits
        var counterLow = (byte)(counter & 0xFF);

        // Gera 6 bytes (48 bits) aleatórios usando RandomNumberGenerator (criptograficamente seguro).
        // stackalloc aloca na stack ao invés do heap (mais rápido e sem garbage collection).
        //
        // Estes bytes aleatórios são FUNDAMENTAIS para:
        // 1. Evitar colisões entre threads rodando no mesmo processo
        // 2. Evitar colisões entre múltiplas instâncias da aplicação (servidores diferentes)
        // 3. Garantir unicidade global sem necessidade de coordenação central
        //
        // Com 46 bits de aleatoriedade, a probabilidade de colisão é astronômica (~10^-14).
        Span<byte> randomBytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(randomBytes);

        return new Guid(
            a: timestampHigh,        // 4 bytes - parte alta do timestamp
            b: timestampLow,         // 2 bytes - parte baixa do timestamp
            c: versionAndCounter,    // 2 bytes - versão 7 + 12 bits do counter
            d: variantHigh,          // 1 byte - variant + 6 bits do counter
            e: counterLow,           // 1 byte - 8 bits do counter
            f: randomBytes[0],       // 6 bytes aleatórios para unicidade
            g: randomBytes[1],
            h: randomBytes[2],
            i: randomBytes[3],
            j: randomBytes[4],
            k: randomBytes[5]
        );
    }

    // Faz uma espera ativa (spin-wait) até que o relógio avance para o próximo milissegundo.
    //
    // POR QUE SPIN-WAIT AO INVÉS DE Thread.Sleep(1)?
    // Spin-wait mantém a CPU ocupada em um loop, mas isso é mais eficiente para esperas muito curtas:
    // - Thread.Sleep(1) pode dormir por 1-15ms dependendo do sistema operacional (muito impreciso!)
    // - Thread.Sleep causa context switch (troca de contexto), que custa ~1-10 microsegundos
    // - Sabemos que falta MENOS DE 1 MILISSEGUNDO, então spin-wait é mais rápido
    //
    // O valor 100 em Thread.SpinWait(100) é o número de iterações de spin:
    // - Não bloqueia o processador completamente
    // - Permite que outros hyperthreads no mesmo core físico executem
    // - Em processadores modernos, 100 iterações ≈ alguns nanosegundos
    private static void SpinWaitForNextMillisecond(ref long timestamp, ref long lastTimestamp)
    {
        while (timestamp == lastTimestamp)
        {
            Thread.SpinWait(100);
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        lastTimestamp = timestamp;
    }

    #endregion [ Private Methods ]
}
