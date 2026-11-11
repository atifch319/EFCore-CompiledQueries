namespace EfCompiledQueries.Api.Models;

public sealed class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>
    /// Price copied from the product when the order was placed. Later catalog
    /// price changes do not rewrite history.
    /// </summary>
    public decimal UnitPrice { get; set; }
}
