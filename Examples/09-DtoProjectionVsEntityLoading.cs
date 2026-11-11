namespace EfCompiledQueries.Examples;

/// <summary>
/// The summary query selects four columns and joins the customer for the name.
/// The graph query loads the order, the customer, every item, and every product.
/// Both are normal LINQ. A compiled query can wrap either shape.
/// Compilation does not remove columns you chose to load.
/// </summary>
public static class DtoProjectionVsEntityLoading
{
    public const string Takeaway =
        "Projection and compiled queries solve different problems. Project the columns you need, whether or not you compile the query.";

    public static async Task<ProjectionComparison> CompareAsync(
        AppDbContext db,
        int orderId,
        CancellationToken cancellationToken)
    {
        var projected = await OrderQueries.NormalByIdAsync(db, orderId, cancellationToken);
        var graph = await OrderQueries.LoadOrderGraphAsync(db, orderId, cancellationToken);

        return new ProjectionComparison(
            projected,
            graph?.Customer.Name,
            graph?.Items.Count ?? 0,
            projected?.CustomerName == graph?.Customer.Name);
    }
}

public sealed record ProjectionComparison(
    OrderSummaryDto? Projected,
    string? GraphCustomerName,
    int GraphItemCount,
    bool CustomerNameMatches);
