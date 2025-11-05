# Princípios de Benchmarking

Não sou especialista em benchmarking e nem tenho a pretenção de medir o desempenho de forma exata. Porém, ao longo do tempo, desenvolvi algumas práticas que me ajudam a obter resultados mais confiáveis e úteis.

Para ter um benchmark preciso é importante ter um ambiente controlado, onde as variáveis que podem afetar o desempenho sejam minimizadas. Isso inclui usar hardware consistente, evitar processos em segundo plano que possam interferir e garantir que o sistema esteja em um estado estável durante os testes.

Porém, não temos isso disponível em nosso dia a dia. Então, o foco aqui é em práticas que podem ser aplicadas em qualquer cenário para obter resultados mais confiáveis.

Os resultados não serão exatos, mas serão suficientemente bons para comparar diferentes abordagens e identificar melhorias de desempenho.

## Princípios

- O benchmark deve executar em modo Release. Isso garante que o código seja otimizado pelo compilador, refletindo melhor o desempenho real.
- Quando estivermos testando um objeto que atua para encapsular algum comportamento nativo do .NET, a baseline deve ser o comportamento nativo. Por exemplo, se estivermos testando uma implementação personalizada de um dicionário, a baseline deve ser o `Dictionary<TKey, TValue>` do .NET.
- O benchmark deve ser executado várias vezes para reduzir a variabilidade dos resultados. Isso ajuda a obter uma média mais representativa do desempenho.
- Embora no cenário real usaremos a otimização que o compilador oferece em modo Release, temos que escrever o método de teste de um modo que o compilador não remova o código por otimização. Por exemplo, se estivermos testando um método que retorna um valor, devemos garantir que esse valor seja usado de alguma forma, para evitar que o compilador elimine a chamada ao método.
- O benchmark é executado sempre com uma única iteração, porém, também incluímos iterações realistas para o propósito do objeto, por exemplo: para testar a classe `CustomDateTimeProvider`, fazemos com 1 iteração, mas também com 5 iterações (simulando 5 chamadas para obter a data/hora atual durante um processamento qualquer).
- O escopo do benchmark deve ser claro e focado. Devemos evitar incluir múltiplas operações ou funcionalidades em um único teste, para garantir que os resultados sejam específicos e relevantes.

## Objetivo

O objetivo principal do benchmark é comparar diferentes implementações ou abordagens para uma mesma funcionalidade, identificando qual delas oferece o melhor desempenho em termos de tempo de execução e uso de recursos.

Temos que equilibrar o rigor científico com a praticidade, buscando resultados que sejam úteis e aplicáveis no contexto real de desenvolvimento de software.

## Como interpretar os resultados

Saber como interpretar os resultados do benchmark é tão importante quanto saber como realizá-lo. Aqui estão alguns pontos-chave para considerar ao analisar os resultados:

Exemplo de resultado:

| Method                                                | IterationCount | Mean      | Error    | StdDev   | Ratio | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------------------------------------------------ |--------------- |----------:|---------:|---------:|------:|---------------------:|-----------------:|----------:|------------:|
| 'From DateTimeOffSet.UtcNow'                          | 1              |  24.88 ns | 0.047 ns | 0.044 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider without Func'                | 1              |  24.91 ns | 0.037 ns | 0.032 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider with Func and fixed value'   | 1              |  24.91 ns | 0.016 ns | 0.015 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider with Func and dynamic value' | 1              |  24.91 ns | 0.031 ns | 0.027 ns |  1.00 |                    - |                - |         - |          NA |
|                                                       |                |           |          |          |       |                      |                  |           |             |
| 'From DateTimeOffSet.UtcNow'                          | 5              | 124.21 ns | 0.367 ns | 0.343 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider without Func'                | 5              | 123.99 ns | 0.138 ns | 0.129 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider with Func and fixed value'   | 5              | 123.71 ns | 0.119 ns | 0.111 ns |  1.00 |                    - |                - |         - |          NA |
| 'From CustomTimeProvider with Func and dynamic value' | 5              | 123.91 ns | 0.121 ns | 0.113 ns |  1.00 |                    - |                - |         - |          NA |

# Legenda de Benchmarking - Tabela de Referência

| Coluna | Explicação |
|--------|-----------|
| IterationCount | Valor do parâmetro 'IterationCount' |
| Mean | Média aritmética de todas as medições |
| Error | Metade do intervalo de confiança de 99,9% |
| StdDev | Desvio padrão de todas as medições |
| Ratio | Média da distribuição de razão ([Atual]/[Baseline]) |
| Completed Work Items | O número de itens de trabalho processados na ThreadPool (por operação única) |
| Lock Contentions | O número de vezes que houve contenção ao tentar adquirir um bloqueio de Monitor (por operação única) |
| Allocated | Memória alocada por operação única (somente gerenciada, inclusiva, 1KB = 1024B) |
| Alloc Ratio | Distribuição de razão de memória alocada ([Atual]/[Baseline]) |
| 1 ns | 1 Nanossegundo (0.000000001 seg) |

Ao analisar os resultados do benchmark, devemos considerar os seguintes aspectos:
- **Média (Mean)**: A média do tempo de execução é o principal indicador de desempenho relacionado à velocidade de execução. Devemos comparar as médias entre diferentes métodos para identificar qual é o mais rápido.
- **Erro (Error)**: O erro indica a precisão da média. Um erro menor sugere que os resultados são mais confiáveis.
- **Desvio Padrão (StdDev)**: O desvio padrão mostra a variabilidade dos resultados. Um desvio padrão baixo indica que os resultados são consistentes.
- **Razão (Ratio)**: A razão compara o desempenho de cada método em relação à baseline. Uma razão menor que 1 indica que o método é mais rápido que a baseline, enquanto uma razão maior que 1 indica que é mais lento. Por exemplo, uma razão de 0.8 significa que o método é 20% mais rápido que a baseline.
- **Aloc ação de Memória (Allocated)**: A quantidade de memória alocada pode impactar o desempenho, especialmente em cenários de alta carga. Devemos considerar métodos que alocam menos memória, desde que o desempenho seja aceitável.
