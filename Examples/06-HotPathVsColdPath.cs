namespace EfCompiledQueries.Examples;

/// <summary>
/// The hot path is the order lookup, called constantly. The cold path is a fixed admin report:
/// cancelled orders in one city. That report is stable enough to compile.
/// It stays as normal LINQ because a few executions a day will not repay the extra code,
/// and the automatic cache already covers the second and later runs.
/// </summary>
public static class HotPathVsColdPath
{
    public const string Takeaway =
        "Frequency decides whether explicit compilation is worth the code. A stable query can still be a poor candidate when it rarely runs.";

    public static async Task<HotAndColdResult> RunAsync(
        AppDbContext db,
        int hotOrderId,
        string city,
        CancellationToken cancellationToken)
    {
        var hot = await OrderQueries.CompiledByIdAsync(db, hotOrderId, cancellationToken);
        var cold = await CancelledOrdersInCityAsync(db, city, cancellationToken);
        return new HotAndColdResult(hot, cold);
    }

    public static Task<List<OrderSummaryDto>> CancelledOrdersInCityAsync(
        AppDbContext db,
        string city,
        CancellationToken cancellationToken) =>
        db.Orders
            .Where(order => order.Status == OrderStatus.Cancelled && order.Customer.City == city)
            .OrderByDescending(order => order.OrderedAtUtc)
            .Select(order => new OrderSummaryDto(
                order.Id,
                order.Customer.Name,
                order.TotalAmount,
                order.Status))
            .ToListAsync(cancellationToken);
}

public sealed record HotAndColdResult(
    OrderSummaryDto? HotPathOrder,
    IReadOnlyList<OrderSummaryDto> ColdPathOrders);
