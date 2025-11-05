using System.Reflection;

namespace Benchmarks;

internal static class Program
{
    private static void Main()
    {
        var lastTypeIndex = 0;

        // Localiza todos os objetos, via reflection, que implementam a interface IBenchmark
        var benchmarkTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IBenchmark).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .OrderBy(t => t.FullName)
            .Select(t => new { 
                Index = lastTypeIndex++,
                Type = t,
                t.FullName
            })
            .ToArray();

        // Escreve os tipos encontrados
        foreach (var benchmarkType in benchmarkTypes)
        {
            Console.WriteLine($"{benchmarkType.Index} - {benchmarkType.FullName}");
        }

        // Solicita para o usuário informar qual index deseja executar
        Console.WriteLine();
        Console.Write("Informe o index do benchmark que deseja executar: ");
        var input = Console.ReadLine();

        // Tenta converter o input para um número inteiro
        if(!int.TryParse(input, out var selectedIndex))
        {
            Console.WriteLine("Input inválido. Encerrando.");
            return;
        }

        // Localiza o tipo selecionado
        var selectedBenchmark = benchmarkTypes.FirstOrDefault(bt => bt.Index == selectedIndex);
        if(selectedBenchmark == null)
        {
            Console.WriteLine("Index não encontrado. Encerrando.");
            return;
        }

        // Executa o benchmark usando BenchmarkRunner
        var benchmarkTypeToRun = selectedBenchmark.Type;
        Console.WriteLine($"Executando benchmark: {selectedBenchmark.FullName}");
        BenchmarkRunner.Run(benchmarkTypeToRun);
    }
}