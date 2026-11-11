namespace EfCompiledQueries.Api.Models;

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public string City { get; set; } = "";

    // Required relationship. EF assigns this when a query loads the customer.
    public List<Order> Orders { get; set; } = [];
}
