namespace EfCompiledQueries.Examples;

/// <summary>
/// Two executions of the same LINQ shape with different ids.
/// The parameter value changes. The operators, the projection, and the filter do not.
/// That is one cache entry, not two compilations.
///
/// A different shape (an extra Where, a different Select, an Include) is a different cache entry.
/// SQL Server has its own plan cache for the parameterized SQL. That is a separate cache.
/// An explicit compiled query skips EF's per-call tree build and lookup. It is not the same mechanism.
/// </summary>
public static class EfCoreQueryCache
{
    public const string Takeaway =
        "Same LINQ shape + different parameter values = one EF query-cache entry. " +
        "Explicit compilation is an extra step on top of that, not the thing that turns caching on.";

    public static async Task<SameShapeResult> RunAsync(
        AppDbContext db,
        int firstId,
        int secondId,
        CancellationToken cancellationToken)
    {
        var first = await OrderQueries.NormalByIdAsync(db, firstId, cancellationToken);
        var second = await OrderQueries.NormalByIdAsync(db, secondId, cancellationToken);
        var compiledFirst = await OrderQueries.CompiledByIdAsync(db, firstId, cancellationToken);

        return new SameShapeResult(first, second, first == compiledFirst);
    }
}

public sealed record SameShapeResult(
    OrderSummaryDto? FirstId,
    OrderSummaryDto? SecondId,
    bool CompiledMatchesFirst);
