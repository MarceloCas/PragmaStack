# Custom Time Provider

A classe `CustomTimeProvider` permite que você defina uma fonte personalizada de tempo para sua aplicação. Isso é útil em cenários onde você deseja controlar o tempo de forma precisa, como em testes de unidade ou simulações.

## Sumário

- [Problema real](#problema-real)
- [Funcionalidades](#funcionalidades)
- [Uso](#uso)
- [Impacto na performance](#impacto-na-performance)

## Problema real

Em muitas aplicações, o tempo é obtido diretamente do sistema operacional, o que pode dificultar testes e simulações. Por exemplo, se você estiver testando uma funcionalidade que depende do tempo, como expiração de sessões ou agendamento de tarefas, pode ser complicado controlar o tempo real. Por exemplo:

### Expirar cadastros desatualizados há 7 dias

Essa funcionalidade precisa verificar cadastros que não foram atualizados nos últimos 7 dias e marcá-los como expirados. Se o tempo for obtido diretamente do sistema, os testes podem se tornar inconsistentes e difíceis de reproduzir.

Imagine ter que fazer um cadastro, esperar 7 dias reais e depois verificar se ele foi expirado corretamente com base na hora do sistema. Isso não é prático para testes automatizados.

Em um teste real e eficiente, precisamos ter o controle sobre o input dado e o output esperado, sem depender do tempo real.

A partir do .net 6, podemos criar um `TimeProvider` personalizado para resolver esse problema. Ao invés de se obter o tempo diretamente da classe `DateTime`, podemos utilizar o `TimeProvider.Current` para obter o tempo atual. Isso nos permite injetar um `CustomTimeProvider` durante os testes, onde podemos definir o tempo conforme necessário.

Exemplo de código utilizando o DateTime padrão:

```csharp
public void ExpireOldRegistrations()
{
    var cutoffDate = DateTime.UtcNow.AddDays(-7);
    var oldRegistrations = _registrationRepository.GetRegistrationsBefore(cutoffDate);
    foreach (var registration in oldRegistrations)
    {
        registration.Expire();
    }
}
```

O código acima depende do tempo real, o que dificulta os testes.

Agora veja como ficaria utilizando o `CustomTimeProvider`:

```csharp
public void ExpireOldRegistrations(TimeProvider timeProvider)
{
    var cutoffDate = timeProvider.GetUtcNow().AddDays(-7);
    var oldRegistrations = _registrationRepository.GetRegistrationsBefore(cutoffDate);
    foreach (var registration in oldRegistrations)
    {
        registration.Expire();
    }
}
```

Podemos, inclusive, receber o TimeProvider via injeção de dependência, facilitando ainda mais os testes. Por exemplo:

```csharp
public class RegistrationService
{
    private readonly TimeProvider _timeProvider;

    public RegistrationService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void ExpireOldRegistrations()
    {
        var cutoffDate = _timeProvider.GetUtcNow().AddDays(-7);
        var oldRegistrations = _registrationRepository.GetRegistrationsBefore(cutoffDate);

        foreach (var registration in oldRegistrations)
        {
            registration.Expire();
        }
    }
}
```

## Funcionalidades

- Pode ser utilizado como fonte de tempo do sistema, retornando a data/hora atual do sistema operacional.
- Pode ser configurado para retornar uma data/hora fixa, útil para testes e simulações
- Pertime informar um timezone customizado para o horário local.
- Instância estática padrão disponível via `CustomTimeProvider.Default` para quem não precisa de customizações ou injeção de dependência.


## Uso

Devido ao problema real explicado na seção anterior, o `CustomTimeProvider` pode ser utilizado para definir um tempo fixo ou simulado. Como ele pode se comprotar obtendo o valor real do sistema ou simulado, ele pode ser usado tanto em produção quanto em testes.

Dependendo da natureza do seu sistema, algumas funcionalidades dependem do tempo, como funcionalidades que envolam Timelines, agendamentos, expirações, entre outros. Nesses casos, o `CustomTimeProvider` pode ser injetado para controlar o tempo de forma precisa e permitir recursos avançados como simulações e testes.

Você pode usar o `CustomTimeProvider` da forma que você desejar, afinal de contas, é uma classe como outra qualquer, porém, recomenda-se o uso dela em conjunto com a injeção de dependência para facilitar o controle do tempo em diferentes ambientes (produção, testes, etc).

### Exemplo de uso sem usar injeção de dependência usando data do sistema
```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: null, localTimeZone: null);
        Console.WriteLine($"Current UTC Time: {timeProvider.GetUtcNow()}");
        Console.WriteLine($"Current Local Time: {timeProvider.GetLocalNow()}");
    }
}
```

### Exemplo de uso sem usar injeção de dependência usando data customizada
```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var expectedTime = new DateTimeOffset(
            year: 2023,
            month: 1,
            day: 1,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero
        );
        Func<TimeZoneInfo?, DateTimeOffset> customUtcNowFunc = (tz) => expectedTime;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: customUtcNowFunc, localTimeZone: null);

        Console.WriteLine($"Custom UTC Time: {timeProvider.GetUtcNow()}");
        Console.WriteLine($"Custom Local Time: {timeProvider.GetLocalNow()}");
    }
}
```

### Exemplo de uso com injeção de dependência usando data do sistema
```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(provider => new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: null, localTimeZone: null));
        var serviceProvider = services.BuildServiceProvider();

        var someService = serviceProvider.GetRequiredService<SomeService>();
        someService.SomeMethod();
    }
}

public class SomeService
{
    private readonly TimeProvider _timeProvider;

    public SomeService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void SomeMethod()
    {
        Console.WriteLine($"Current UTC Time: {_timeProvider.GetUtcNow()}");
        Console.WriteLine($"Current Local Time: {_timeProvider.GetLocalNow()}");
    }
}
```

### Exemplo de uso com injeção de dependência usando data customizada
```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var expectedTime = new DateTimeOffset(
            year: 2023,
            month: 1,
            day: 1,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero
        );
        Func<TimeZoneInfo?, DateTimeOffset> customUtcNowFunc = (tz) => expectedTime;

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(provider => new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: customUtcNowFunc, localTimeZone: null));
        var serviceProvider = services.BuildServiceProvider();

        var someService = serviceProvider.GetRequiredService<SomeService>();
        someService.SomeMethod();
    }
}

public class SomeService
{
    private readonly TimeProvider _timeProvider;

    public SomeService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void SomeMethod()
    {
        Console.WriteLine($"Custom UTC Time: {_timeProvider.GetUtcNow()}");
        Console.WriteLine($"Custom Local Time: {_timeProvider.GetLocalNow()}");
    }
}
```

## Impacto na performance

Inicialmente, podemos acreditar que o uso de um TimeProvider pode ocasionar uma perda de performance significativa, porém, na prática, o impacto é mínimo e muitas vezes imperceptível. A abstração do tempo através de um TimeProvider pode introduzir uma leve sobrecarga devido à chamada de métodos adicionais, mas essa sobrecarga é geralmente insignificante em comparação com os benefícios que ele traz, especialmente em termos de testabilidade e flexibilidade do código.

Além disso, o uso de um TimeProvider pode melhorar a performance geral do sistema em cenários onde o tempo precisa ser manipulado ou simulado, como em testes automatizados. Ao permitir que o tempo seja controlado de forma programática, podemos evitar esperas desnecessárias e tornar os testes mais rápidos e eficientes.

Porém, sempre tem a grande dúvida: "Será que o uso do TimeProvider impacta a performance da minha aplicação?", então vamos fazer um benchmark simples, mas que ajudará a entender o impacto do uso do TimeProvider na performance. Esse código fonte desse benchmark pode ser encontrado, a partir da raiz do projeto, na classe `tests\Benchmarks\Benchs\TimeProvidersBenchs\CustomTimeProviderBench.cs`.

Esse benchmark compara o desempenho do `CustomTimeProvider` com o uso direto do `DateTime.UtcNow`.

### Resultados do Benchmark
```bash
// * Summary *

BenchmarkDotNet v0.15.5, Windows 11 (10.0.26200.7019)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.100-rc.2.25502.107
  [Host]     : .NET 10.0.0 (10.0.0-rc.2.25502.107, 10.0.25.50307), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.0 (10.0.0-rc.2.25502.107, 10.0.25.50307), X64 RyuJIT x86-64-v3


| Method                                                | IterationCount | Mean      | Error    | StdDev   | Ratio | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------------------------------------------------ |--------------- |----------:|---------:|---------:|------:|---------------------:|-----------------:|----------:|------------:|
| 'From DateTimeOffSet.UtcNow'                          | 1              |  24.82 ns | 0.013 ns | 0.011 ns |  1.00 |                    - |                - |         - |          NA |
| 'From static default instance'                        | 1              |  25.23 ns | 0.012 ns | 0.010 ns |  1.02 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider without Func'                | 1              |  24.83 ns | 0.006 ns | 0.005 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider with Func and fixed value'   | 1              |  24.89 ns | 0.018 ns | 0.016 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider with Func and dynamic value' | 1              |  24.87 ns | 0.016 ns | 0.014 ns |  1.00 |                    - |                - |         - |          NA |
|                                                       |                |           |          |          |       |                      |                  |           |             |
| 'From DateTimeOffSet.UtcNow'                          | 5              | 124.00 ns | 0.177 ns | 0.166 ns |  1.00 |                    - |                - |         - |          NA |
| 'From static default instance'                        | 5              | 124.63 ns | 0.177 ns | 0.148 ns |  1.01 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider without Func'                | 5              | 123.73 ns | 0.045 ns | 0.038 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider with Func and fixed value'   | 5              | 123.81 ns | 0.098 ns | 0.082 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider with Func and dynamic value' | 5              | 123.86 ns | 0.097 ns | 0.081 ns |  1.00 |                    - |                - |         - |          NA |

// * Hints *
Outliers
  CustomTimeProviderBench.'From DateTimeOffSet.UtcNow': Default                          -> 2 outliers were removed (26.03 ns, 26.17 ns)
  CustomTimeProviderBench.'From static default instance': Default                        -> 2 outliers were removed (26.41 ns, 26.56 ns)
  CustomTimeProviderBench.'From CustomTimeProvider without Func': Default                -> 2 outliers were removed (26.00 ns, 26.01 ns)
  CustomTimeProviderBench.'From CustomTimeProvider with Func and fixed value': Default   -> 1 outlier  was  removed (26.16 ns)
  CustomTimeProviderBench.'From CustomTimeProvider with Func and dynamic value': Default -> 1 outlier  was  removed (26.13 ns)
  CustomTimeProviderBench.'From static default instance': Default                        -> 2 outliers were removed (126.90 ns, 126.92 ns)
  CustomTimeProviderBench.'From CustomTimeProvider without Func': Default                -> 2 outliers were removed (125.78 ns, 126.02 ns)
  CustomTimeProviderBench.'From CustomTimeProvider with Func and fixed value': Default   -> 2 outliers were removed, 4 outliers were detected (125.26 ns, 125.35 ns, 125.62 ns, 125.79 ns)
  CustomTimeProviderBench.'From CustomTimeProvider with Func and dynamic value': Default -> 2 outliers were removed (126.36 ns, 128.35 ns)

// * Legends *
  IterationCount       : Value of the 'IterationCount' parameter
  Mean                 : Arithmetic mean of all measurements
  Error                : Half of 99.9% confidence interval
  StdDev               : Standard deviation of all measurements
  Ratio                : Mean of the ratio distribution ([Current]/[Baseline])
  Completed Work Items : The number of work items that have been processed in ThreadPool (per single operation)
  Lock Contentions     : The number of times there was contention upon trying to take a Monitor's lock (per single operation)
  Allocated            : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
  Alloc Ratio          : Allocated memory ratio distribution ([Current]/[Baseline])
  1 ns                 : 1 Nanosecond (0.000000001 sec)

```

A partir desse resultado, podemos concluir algumas coisas importantes:

- O uso do `CustomTimeProvider` não introduz uma sobrecarga significativa em comparação com o uso direto do `DateTime.UtcNow`. As diferenças de tempo são mínimas e, na maioria dos casos, imperceptíveis.
- A diferença de tempo de execução é em nanosegundos, o que é extremamente rápido e não deve impactar o desempenho geral da aplicação.
- Não houve alocação de memória adicional ao usar o `CustomTimeProvider`, o que é um ponto positivo em termos de eficiência.
- Quando usamos a função com o tempo dinâmico, ou seja, obtendo o tempo atual do sistema, 
o desempenho é praticamente idêntico ao uso direto do `DateTime.UtcNow`.
- Mesmo que a instância estática padrão seja ligeiramente mais lenta, a diferença é tão pequena que, na prática, não faz diferença.

Portanto, podemos concluir que o uso do `CustomTimeProvider` é uma prática recomendada para melhorar a testabilidade e flexibilidade do código, sem comprometer o desempenho da aplicação.
