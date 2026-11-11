using EfCompiledQueries.Api.Data;
using EfCompiledQueries.Api.Dtos;
using EfCompiledQueries.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCompiledQueries.Api.Queries;

/// <summary>
/// Single definition of the order queries used by the API, tests, benchmarks,
/// and the Examples project.
///
/// Normal LINQ builds an expression tree on each call. EF Core hashes the shape
/// of that tree (parameter values removed) and reuses a cached translation after
/// the first execution. A normal query is not recompiled from scratch every time.
///
/// EF.CompileQuery / EF.CompileAsyncQuery build a delegate once. Later calls skip
/// the per-call tree build and cache lookup. They still send parameterized SQL
/// to SQL Server on every call. They do not cache result rows.
///
/// Keep each normal query and its compiled twin visually identical.
/// OrderQueryTests fails if they drift.
///
/// The delegate is bound to the EF model from the first context that executes it.
/// Use one model per process. Building AppDbContext twice with different options
/// throws "executed with a different model than it was compiled against."
/// </summary>
public static class OrderQueries
{
    public static Task<OrderSummaryDto?> NormalByIdAsync(
        AppDbContext db,
        int id,
        CancellationToken cancellationToken) =>
        db.Orders
            .Where(order => order.Id == id)
            .Select(order => new OrderSummaryDto(
                order.Id,
                order.Customer.Name,
                order.TotalAmount,
                order.Status))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Synchronous compiled query. FirstOrDefault is required here.
    /// FirstOrDefaultAsync is not a supported operator inside a compiled query expression.
    /// The API does not call this on a request thread; it calls <see cref="CompiledByIdAsync"/>.
    /// </summary>
    public static readonly Func<AppDbContext, int, OrderSummaryDto?> CompiledById =
        EF.CompileQuery((AppDbContext db, int id) =>
            db.Orders
                .Where(order => order.Id == id)
                .Select(order => new OrderSummaryDto(
                    order.Id,
                    order.Customer.Name,
                    order.TotalAmount,
                    order.Status))
                .FirstOrDefault());

    /// <summary>
    /// Asynchronous single-row compiled query. The cancellation token is a delegate
    /// parameter, not an operator inside the LINQ expression. EF Core uses it to
    /// cancel the command. The body still uses the synchronous FirstOrDefault operator.
    /// </summary>
    public static readonly Func<AppDbContext, int, CancellationToken, Task<OrderSummaryDto?>> CompiledByIdAsync =
        EF.CompileAsyncQuery((AppDbContext db, int id, CancellationToken cancellationToken) =>
            db.Orders
                .Where(order => order.Id == id)
                .Select(order => new OrderSummaryDto(
                    order.Id,
                    order.Customer.Name,
                    order.TotalAmount,
                    order.Status))
                .FirstOrDefault());

    public static async Task<IReadOnlyList<OrderSummaryDto>?> NormalByCustomerAsync(
        AppDbContext db,
        int customerId,
        CancellationToken cancellationToken)
    {
        var orders = await db.Orders
            .Where(order => order.CustomerId == customerId)
            .OrderBy(order => order.Id)
            .Select(order => new OrderSummaryDto(
                order.Id,
                order.Customer.Name,
                order.TotalAmount,
                order.Status))
            .ToListAsync(cancellationToken);

        // One round trip when the customer has orders. The exists check runs only
        // for the empty result, so a missing customer (404) is distinct from a
        // customer who has not ordered yet (200 and an empty list).
        if (orders.Count > 0 || await CustomerExistsAsync(db, customerId, cancellationToken))
            return orders;

        return null;
    }

    /// <summary>
    /// Sequence-shaped async compiled query. Invoke it and read with await foreach.
    /// Do not call ToListAsync on the delegate; it is not an IQueryable.
    /// </summary>
    public static readonly Func<AppDbContext, int, IAsyncEnumerable<OrderSummaryDto>> CompiledByCustomer =
        EF.CompileAsyncQuery((AppDbContext db, int customerId) =>
            db.Orders
                .Where(order => order.CustomerId == customerId)
                .OrderBy(order => order.Id)
                .Select(order => new OrderSummaryDto(
                    order.Id,
                    order.Customer.Name,
                    order.TotalAmount,
                    order.Status)));

    public static async Task<IReadOnlyList<OrderSummaryDto>?> CompiledByCustomerAsync(
        AppDbContext db,
        int customerId,
        CancellationToken cancellationToken)
    {
        var orders = new List<OrderSummaryDto>();
        await foreach (var order in CompiledByCustomer(db, customerId).WithCancellation(cancellationToken))
            orders.Add(order);

        if (orders.Count > 0 || await CustomerExistsAsync(db, customerId, cancellationToken))
            return orders;

        return null;
    }

    public static Task<bool> CustomerExistsAsync(
        AppDbContext db,
        int customerId,
        CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken);

    /// <summary>
    /// Read that loads the order, customer, items, and products.
    /// AsNoTracking because this is a read: tracking would keep the graph alive
    /// for the rest of the context lifetime. Projection avoids that work entirely.
    /// Compiling this query would not stop it from loading the graph.
    /// </summary>
    public static Task<Order?> LoadOrderGraphAsync(
        AppDbContext db,
        int id,
        CancellationToken cancellationToken) =>
        db.Orders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public static async Task<List<ProductSalesDto>> ProductSalesNormalAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await ProductSalesQuery(db).ToListAsync(cancellationToken);
        rows.Sort(CompareSales);
        return rows;
    }

    /// <summary>
    /// Same grouping as <see cref="ProductSalesNormalAsync"/>. The final sort of the
    /// few product rows happens in memory: EF Core 10 does not translate the ordered
    /// DTO projection inside this compiled delegate. SQL Server still performs the sums.
    /// </summary>
    public static readonly Func<AppDbContext, IAsyncEnumerable<ProductSalesDto>> CompiledProductSales =
        EF.CompileAsyncQuery((AppDbContext db) =>
            db.OrderItems
                .GroupBy(item => item.Product.Name)
                .Select(group => new ProductSalesDto(
                    group.Key,
                    group.Sum(item => item.Quantity),
                    group.Sum(item => item.Quantity * item.UnitPrice))));

    public static async Task<List<ProductSalesDto>> ProductSalesCompiledAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = new List<ProductSalesDto>();
        await foreach (var row in CompiledProductSales(db).WithCancellation(cancellationToken))
            rows.Add(row);

        rows.Sort(CompareSales);
        return rows;
    }

    private static IQueryable<ProductSalesDto> ProductSalesQuery(AppDbContext db) =>
        db.OrderItems
            .GroupBy(item => item.Product.Name)
            .Select(group => new ProductSalesDto(
                group.Key,
                group.Sum(item => item.Quantity),
                group.Sum(item => item.Quantity * item.UnitPrice)));

    private static int CompareSales(ProductSalesDto left, ProductSalesDto right)
    {
        var byRevenue = right.Revenue.CompareTo(left.Revenue);
        return byRevenue != 0
            ? byRevenue
            : string.Compare(left.ProductName, right.ProductName, StringComparison.Ordinal);
    }
}
