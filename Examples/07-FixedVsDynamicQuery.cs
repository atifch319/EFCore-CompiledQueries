namespace EfCompiledQueries.Examples;

/// <summary>
/// The by-id delegate has one shape forever: filter on id, project four columns.
/// The search method adds Where clauses only for the filters the caller set.
/// Each combination is a different expression tree, so one compiled delegate cannot represent it.
/// EF's automatic cache stores a separate entry per shape that actually runs.
///
/// You can force a single shape with predicates such as "status is null OR status equals the parameter".
/// That compiles, and it can also produce a worse SQL plan. Dynamic LINQ is often the better fit.
/// </summary>
public static class FixedVsDynamicQuery
{
    public const string Takeaway =
        "Compile a query whose shape does not change. Leave highly dynamic composition as normal LINQ.";

    public static OrderSummaryDto? FixedById(AppDbContext db, int id) =>
        OrderQueries.CompiledById(db, id);

    public static Task<List<OrderSummaryDto>> DynamicSearchAsync(
        AppDbContext db,
        OrderSearch search,
        CancellationToken cancellationToken)
    {
        IQueryable<Order> query = db.Orders;

        if (search.CustomerId is int customerId)
            query = query.Where(order => order.CustomerId == customerId);

        if (search.Status is OrderStatus status)
            query = query.Where(order => order.Status == status);

        if (search.OrderedFromUtc is DateTime fromUtc)
            query = query.Where(order => order.OrderedAtUtc >= fromUtc);

        if (search.OrderedToUtc is DateTime toUtc)
            query = query.Where(order => order.OrderedAtUtc < toUtc);

        return query
            .OrderBy(order => order.Id)
            .Select(order => new OrderSummaryDto(
                order.Id,
                order.Customer.Name,
                order.TotalAmount,
                order.Status))
            .ToListAsync(cancellationToken);
    }
}

public sealed record OrderSearch(
    int? CustomerId,
    OrderStatus? Status,
    DateTime? OrderedFromUtc,
    DateTime? OrderedToUtc);
