using BenchmarkDotNet.Running;
using EfCompiledQueries.Benchmarks;

if (!await BenchmarkDbContext.ServerIsReachableAsync())
{
    Console.Error.WriteLine("SQL Server is not reachable at localhost with Windows authentication.");
    Console.Error.WriteLine("Benchmarks were not executed. Start SQL Server and rerun:");
    Console.Error.WriteLine("  dotnet run --project benchmarks/EfCompiledQueries.Benchmarks -c Release");
    return 2;
}

if (args.Length == 0)
{
    // Steady-state classes only. FirstExecutionBenchmarks is a separate process
    // so its single call is not warmed by these runs.
    BenchmarkRunner.Run<SteadyStateBenchmarks>();
    BenchmarkRunner.Run<DatasetSizeBenchmarks>();
    return 0;
}

BenchmarkSwitcher.FromAssembly(typeof(SteadyStateBenchmarks).Assembly).Run(args);
return 0;
