namespace EfCompiledQueries.Examples;

/// <summary>
/// A normal EF Core LINQ query. The operators build an expression tree.
/// Nothing is sent to SQL Server until FirstOrDefaultAsync executes the tree.
/// EF Core then caches the translation by the shape of the tree, with the id removed.
/// The next call with a different id reuses that translation. It does not compile from scratch.
/// </summary>
public static class NormalEfCoreQuery
{
    public const string Takeaway =
        "Normal LINQ is already cached by query shape after the first execution. " +
        "Do not describe it as recompiling on every call.";

    public static Task<OrderSummaryDto?> ByIdAsync(
        AppDbContext db,
        int id,
        CancellationToken cancellationToken) =>
        OrderQueries.NormalByIdAsync(db, id, cancellationToken);
}
