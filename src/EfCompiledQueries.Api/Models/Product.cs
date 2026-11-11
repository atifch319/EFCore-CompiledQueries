namespace EfCompiledQueries.Api.Models;

public sealed class Product
{
    public int Id { get; set; }

    public string Sku { get; set; } = "";

    public string Name { get; set; } = "";

    public decimal UnitPrice { get; set; }

    public List<OrderItem> OrderItems { get; set; } = [];
}
