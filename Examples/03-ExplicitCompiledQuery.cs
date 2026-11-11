namespace EfCompiledQueries.Examples;

/// <summary>
/// Synchronous compiled query. The delegate is created by EF.CompileQuery in OrderQueries.
/// Calling it does not rebuild the LINQ tree and does not look up the query cache.
/// It still opens a command and waits for SQL Server before it returns.
/// Do not call this from an ASP.NET request thread. The API uses the async delegate.
/// </summary>
public static class ExplicitCompiledQuery
{
    public const string Takeaway =
        "EF.CompileQuery returns a synchronous delegate. Use FirstOrDefault inside the expression, never FirstOrDefaultAsync.";

    public static OrderSummaryDto? ById(AppDbContext db, int id) =>
        OrderQueries.CompiledById(db, id);
}
