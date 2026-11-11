using EfCompiledQueries.Api.Data;
using EfCompiledQueries.Api.Dtos;
using EfCompiledQueries.Api.Queries;

namespace EfCompiledQueries.Api.Services;

public sealed class OrderService(AppDbContext db, ILogger<OrderService> logger)
{
    public async Task<OrderSummaryDto?> GetOrderNormalAsync(int id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order lookup {OrderId} using {QueryMode}", id, "normal");
        var order = await OrderQueries.NormalByIdAsync(db, id, cancellationToken);
        logger.LogInformation("Order lookup {OrderId} finished. Found {Found}", id, order is not null);
        return order;
    }

    public async Task<OrderSummaryDto?> GetOrderCompiledAsync(int id, CancellationToken cancellationToken)
    {
        // Async compiled delegate: the request thread is not blocked on the SQL round trip.
        // The synchronous CompiledById delegate is the same query for sync callers and benchmarks.
        logger.LogInformation("Order lookup {OrderId} using {QueryMode}", id, "compiled");
        var order = await OrderQueries.CompiledByIdAsync(db, id, cancellationToken);
        logger.LogInformation("Order lookup {OrderId} finished. Found {Found}", id, order is not null);
        return order;
    }

    public async Task<IReadOnlyList<OrderSummaryDto>?> GetCustomerOrdersNormalAsync(
        int customerId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Customer order list {CustomerId} using {QueryMode}", customerId, "normal");
        return await OrderQueries.NormalByCustomerAsync(db, customerId, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderSummaryDto>?> GetCustomerOrdersCompiledAsync(
        int customerId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Customer order list {CustomerId} using {QueryMode}", customerId, "compiled");
        return await OrderQueries.CompiledByCustomerAsync(db, customerId, cancellationToken);
    }

    public BenchmarkInfoDto GetBenchmarkInfo() => new(
        Lesson: "Compiled queries optimize EF overhead, not your entire database workload.",
        MeasuredTimeIncludesSqlServerExecution: true,
        MeasuredTimeIncludesNetworkRoundTrip: true,
        MeasuredTimeIncludesHttpPipeline: false,
        HowToRun: "dotnet run --project benchmarks/EfCompiledQueries.Benchmarks -c Release",
        ComparedApproaches:
        [
            "Normal LINQ order lookup",
            "Explicit compiled order lookup",
            "Repeated execution on one DbContext",
            "DTO projection versus loading the order graph",
            "Product-sales aggregate at two dataset sizes, normal and compiled"
        ],
        Interpretation: "The benchmark clock includes SQL Server execution and the localhost round trip. It does not include HTTP, JSON, or logging. A small difference between normal and compiled lookups means EF overhead was a small part of the call. Read Mean, Error, StdDev, Allocated, and Op/s from your own run. This API does not embed benchmark numbers.");
}
