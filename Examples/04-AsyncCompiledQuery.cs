namespace EfCompiledQueries.Examples;

/// <summary>
/// EF.CompileAsyncQuery of an IQueryable returns IAsyncEnumerable, not a Task and not an IQueryable.
/// Read it with await foreach. WithCancellation passes the request token to the command.
/// The single-row async form is different: CompileAsyncQuery with FirstOrDefault returns Task of the row.
/// </summary>
public static class AsyncCompiledQuery
{
    public const string Takeaway =
        "A compiled sequence is an IAsyncEnumerable. Enumerate it with await foreach. " +
        "An empty sequence is not proof the customer is missing; the API checks that separately.";

    public static async Task<IReadOnlyList<OrderSummaryDto>> ByCustomerAsync(
        AppDbContext db,
        int customerId,
        CancellationToken cancellationToken)
    {
        var orders = new List<OrderSummaryDto>();
        await foreach (var order in OrderQueries.CompiledByCustomer(db, customerId).WithCancellation(cancellationToken))
            orders.Add(order);

        return orders;
    }
}
