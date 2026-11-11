using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using EfCompiledQueries.Api.Data;
using EfCompiledQueries.Api.Queries;

namespace EfCompiledQueries.Benchmarks;

/// <summary>
/// Steady-state comparison. Warmup runs before the measured iterations, so the
/// reported mean is not the first-ever translation of the query.
///
/// Every measured call includes SQL Server execution and the localhost round trip.
/// It does not include HTTP, JSON serialization, or logging.
/// Normal and compiled methods use the same id, the same projection, and the same database.
/// </summary>
[Config(typeof(SteadyStateConfig))]
public class SteadyStateBenchmarks
{
    public const int CallsPerRepeatedIteration = 20;

    private int _orderId;
    private AppDbContext _shared = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        await BenchmarkDbContext.ResetAsync(BenchmarkDbContext.SteadyStateOrderCount);
        _orderId = BenchmarkDbContext.SampleOrderId;
        _shared = BenchmarkDbContext.Create();
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _shared.DisposeAsync();

    [Benchmark]
    public async Task<int> Normal_NewContext()
    {
        await using var db = BenchmarkDbContext.Create();
        var order = await OrderQueries.NormalByIdAsync(db, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> Compiled_NewContext()
    {
        await using var db = BenchmarkDbContext.Create();
        var order = await OrderQueries.CompiledByIdAsync(db, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> Normal_ReusedContext()
    {
        var order = await OrderQueries.NormalByIdAsync(_shared, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> Compiled_ReusedContext()
    {
        var order = await OrderQueries.CompiledByIdAsync(_shared, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> Repeated_Normal_SameContext()
    {
        var last = 0;
        for (var i = 0; i < CallsPerRepeatedIteration; i++)
        {
            var order = await OrderQueries.NormalByIdAsync(_shared, _orderId, CancellationToken.None);
            last = order?.Id ?? 0;
        }

        return last;
    }

    [Benchmark]
    public async Task<int> Repeated_Compiled_SameContext()
    {
        var last = 0;
        for (var i = 0; i < CallsPerRepeatedIteration; i++)
        {
            var order = await OrderQueries.CompiledByIdAsync(_shared, _orderId, CancellationToken.None);
            last = order?.Id ?? 0;
        }

        return last;
    }

    [Benchmark]
    public async Task<int> Projection_ById()
    {
        await using var db = BenchmarkDbContext.Create();
        var order = await OrderQueries.NormalByIdAsync(db, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> EntityGraph_ById()
    {
        await using var db = BenchmarkDbContext.Create();
        var order = await OrderQueries.LoadOrderGraphAsync(db, _orderId, CancellationToken.None);
        return order?.Items.Count ?? 0;
    }
}

/// <summary>
/// Same queries at two row counts. The lookup is a primary-key read.
/// The product-sales query aggregates every order item.
/// GlobalSetup reseeds when <see cref="OrderCount"/> changes. Seeding is not measured.
/// </summary>
[Config(typeof(SteadyStateConfig))]
public class DatasetSizeBenchmarks
{
    [Params(200, 2000)]
    public int OrderCount { get; set; }

    private int _orderId;

    [GlobalSetup]
    public async Task Setup()
    {
        await BenchmarkDbContext.ResetAsync(OrderCount);
        _orderId = BenchmarkDbContext.SampleOrderId;
    }

    [Benchmark]
    public async Task<int> Lookup_Normal()
    {
        await using var db = BenchmarkDbContext.Create();
        var order = await OrderQueries.NormalByIdAsync(db, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> Lookup_Compiled()
    {
        await using var db = BenchmarkDbContext.Create();
        var order = await OrderQueries.CompiledByIdAsync(db, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> Heavy_Normal()
    {
        await using var db = BenchmarkDbContext.Create();
        var sales = await OrderQueries.ProductSalesNormalAsync(db, CancellationToken.None);
        return sales.Count;
    }

    [Benchmark]
    public async Task<int> Heavy_Compiled()
    {
        await using var db = BenchmarkDbContext.Create();
        var sales = await OrderQueries.ProductSalesCompiledAsync(db, CancellationToken.None);
        return sales.Count;
    }
}

/// <summary>
/// One invocation and no warmup. Run this class by itself so an earlier benchmark
/// has not already translated the shape:
/// dotnet run -c Release -- --filter *FirstExecution*
/// The sample includes the first EF translation of that method plus SQL Server and the network.
/// It is one observation, not a steady-state mean.
/// </summary>
[Config(typeof(FirstExecutionConfig))]
public class FirstExecutionBenchmarks
{
    private int _orderId;

    [GlobalSetup]
    public async Task Setup()
    {
        await BenchmarkDbContext.ResetAsync(200);
        _orderId = BenchmarkDbContext.SampleOrderId;
    }

    [Benchmark]
    public async Task<int> Normal_FirstCall()
    {
        await using var db = BenchmarkDbContext.Create();
        var order = await OrderQueries.NormalByIdAsync(db, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> Compiled_FirstCall()
    {
        await using var db = BenchmarkDbContext.Create();
        var order = await OrderQueries.CompiledByIdAsync(db, _orderId, CancellationToken.None);
        return order?.Id ?? 0;
    }
}

public sealed class SteadyStateConfig : ManualConfig
{
    public SteadyStateConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddColumn(RankColumn.Arabic);
        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.Method));
        AddJob(Job.Default
            .WithWarmupCount(3)
            .WithIterationCount(5)
            .WithInvocationCount(1)
            .WithUnrollFactor(1)
            .WithStrategy(RunStrategy.Throughput)
            .WithId("Steady"));
    }
}

public sealed class FirstExecutionConfig : ManualConfig
{
    public FirstExecutionConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddJob(Job.Default
            .WithStrategy(RunStrategy.ColdStart)
            .WithWarmupCount(0)
            .WithIterationCount(1)
            .WithInvocationCount(1)
            .WithUnrollFactor(1)
            .WithId("FirstExecution"));
    }
}
