namespace EfCompiledQueries.Examples;

/// <summary>
/// Two queries against the same database.
/// The lookup reads one order by primary key. SQL Server does very little.
/// The product-sales query groups every order item. SQL Server does the real work.
///
/// This file does not time them. A single stopwatch reading is not a benchmark,
/// and this repository does not invent timing numbers. Run the benchmark project.
/// Compiling either query removes EF lookup overhead and leaves the SQL cost in place.
/// </summary>
public static class DatabaseCostVsEfOverhead
{
    public const string Takeaway =
        "When the database dominates the call, a compiled query can only shave the EF portion. Optimize the SQL and the indexes first, then measure.";

    public static async Task<DatabaseCostComparison> RunAsync(
        AppDbContext db,
        int orderId,
        CancellationToken cancellationToken)
    {
        var singleOrder = await OrderQueries.NormalByIdAsync(db, orderId, cancellationToken);
        var productSales = await OrderQueries.ProductSalesNormalAsync(db, cancellationToken);
        return new DatabaseCostComparison(singleOrder, productSales);
    }
}

public sealed record DatabaseCostComparison(
    OrderSummaryDto? SingleOrder,
    IReadOnlyList<ProductSalesDto> ProductSales);
