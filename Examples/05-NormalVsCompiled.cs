namespace EfCompiledQueries.Examples;

/// <summary>
/// Same database, same id, same projection. Three ways to run that one shape.
/// Equivalence is the point of this file. Speed is the point of the benchmark project.
/// The synchronous call blocks the caller until SQL Server responds.
/// </summary>
public static class NormalVsCompiled
{
    public const string Takeaway =
        "Normal LINQ, the async compiled delegate, and the sync compiled delegate return the same row. " +
        "Choose the async delegate on a request thread. Measure before preferring either over normal LINQ.";

    public static async Task<QueryComparison> CompareAsync(
        AppDbContext db,
        int id,
        CancellationToken cancellationToken)
    {
        var normal = await OrderQueries.NormalByIdAsync(db, id, cancellationToken);
        var compiledAsync = await OrderQueries.CompiledByIdAsync(db, id, cancellationToken);
        var compiledSync = OrderQueries.CompiledById(db, id);

        return new QueryComparison(
            normal,
            compiledAsync,
            compiledSync,
            normal == compiledAsync && normal == compiledSync);
    }
}

public sealed record QueryComparison(
    OrderSummaryDto? Normal,
    OrderSummaryDto? CompiledAsync,
    OrderSummaryDto? CompiledSync,
    bool AllMatch);
