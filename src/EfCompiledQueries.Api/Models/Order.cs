namespace EfCompiledQueries.Api.Models;

public sealed class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public DateTime OrderedAtUtc { get; set; }

    public OrderStatus Status { get; set; }

    /// <summary>
    /// Snapshot of the item totals at purchase time. The hot-path lookup reads
    /// this column instead of summing line items on every request.
    /// </summary>
    public decimal TotalAmount { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}
