using EfCompiledQueries.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EfCompiledQueries.Api.Data;

public enum SeedProfile
{
    /// <summary>Four customers and four orders with hand-checked totals. Used by tests.</summary>
    Tests,

    /// <summary>A few hundred orders for clicking through the API.</summary>
    Development,

    /// <summary>Row count is chosen by the benchmark. The benchmark database is separate.</summary>
    Benchmark
}

public static class DatabaseSeeder
{
    public const int DevelopmentOrderCount = 300;

    private static readonly (string Name, string Email, string City)[] People =
    [
        ("Ava Patel", "ava.patel@example.test", "Seattle"),
        ("Noah Kim", "noah.kim@example.test", "Austin"),
        ("Maya Chen", "maya.chen@example.test", "Denver"),
        ("Leo Martins", "leo.martins@example.test", "Boston"),
        ("Sofia Alvarez", "sofia.alvarez@example.test", "Miami"),
        ("Jonah Wright", "jonah.wright@example.test", "Portland"),
        ("Priya Shah", "priya.shah@example.test", "Chicago"),
        ("Elena Rossi", "elena.rossi@example.test", "Denver"),
        ("Hugo Berg", "hugo.berg@example.test", "Minneapolis"),
        ("Amira Hassan", "amira.hassan@example.test", "Austin"),
        ("Chris Dalton", "chris.dalton@example.test", "Seattle"),
        ("Nina Kowalski", "nina.kowalski@example.test", "Boston")
    ];

    private static readonly (string Sku, string Name, decimal Price)[] Catalog =
    [
        ("NB-100", "Field Notes Notebook", 8.50m),
        ("LP-200", "Cedar Desk Lamp", 24.00m),
        ("CB-300", "Northwind Coffee Beans", 16.00m),
        ("PN-400", "Harbor Pen", 12.00m),
        ("MG-500", "Stoneware Mug", 14.00m),
        ("BK-600", "Canvas Backpack", 54.00m),
        ("HD-700", "Linen Headphones Rest", 19.00m),
        ("KB-800", "Maple Keyboard", 89.00m),
        ("MS-900", "Quiet Mouse", 29.00m),
        ("CH-010", "Wool Chair Pad", 36.00m)
    ];

    public static async Task EnsureCreatedAndSeedAsync(
        AppDbContext db,
        SeedProfile profile,
        ILogger logger,
        int benchmarkOrderCount = 0,
        CancellationToken cancellationToken = default)
    {
        var created = await db.Database.EnsureCreatedAsync(cancellationToken);
        logger.LogInformation(
            "Database {Database} on {DataSource} is ready. Schema was created this call: {Created}",
            db.Database.GetDbConnection().Database,
            db.Database.GetDbConnection().DataSource,
            created);

        if (await db.Customers.AnyAsync(cancellationToken))
        {
            logger.LogInformation(
                "Database {Database} already has customers. Seed skipped. Drop the database to reseed",
                db.Database.GetDbConnection().Database);
            return;
        }

        if (profile == SeedProfile.Tests)
            await SeedFixedSampleAsync(db, cancellationToken);
        else
        {
            var orderCount = profile == SeedProfile.Benchmark
                ? benchmarkOrderCount
                : DevelopmentOrderCount;

            if (orderCount < 1)
                throw new ArgumentOutOfRangeException(nameof(benchmarkOrderCount), "Benchmark seed requires at least one order.");

            await SeedGeneratedOrdersAsync(db, orderCount, cancellationToken);
        }

        logger.LogInformation(
            "Seeded {Profile} data. Customers: {Customers}, products: {Products}, orders: {Orders}",
            profile,
            await db.Customers.CountAsync(cancellationToken),
            await db.Products.CountAsync(cancellationToken),
            await db.Orders.CountAsync(cancellationToken));
    }

    /// <summary>
    /// Known sample. Order totals are the sum of the line items:
    /// Ava paid 41.00, Ava shipped 16.00, Noah pending 48.00, Maya cancelled 24.00.
    /// Leo has no orders, which lets tests tell "empty" apart from "missing customer".
    /// </summary>
    private static async Task SeedFixedSampleAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var ava = Person(0);
        var noah = Person(1);
        var maya = Person(2);
        var leo = Person(3);
        var notebook = Product(0);
        var lamp = Product(1);
        var coffee = Product(2);

        db.Customers.AddRange(ava, noah, maya, leo);
        db.Products.AddRange(notebook, lamp, coffee);

        db.Orders.AddRange(
            Order(ava, new DateTime(2026, 3, 2, 15, 0, 0, DateTimeKind.Utc), OrderStatus.Paid,
                Line(notebook, 2), Line(lamp, 1)),
            Order(ava, new DateTime(2026, 4, 11, 15, 0, 0, DateTimeKind.Utc), OrderStatus.Shipped,
                Line(coffee, 1)),
            Order(noah, new DateTime(2026, 5, 20, 15, 0, 0, DateTimeKind.Utc), OrderStatus.Pending,
                Line(coffee, 3)),
            Order(maya, new DateTime(2026, 6, 1, 15, 0, 0, DateTimeKind.Utc), OrderStatus.Cancelled,
                Line(lamp, 1)));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedGeneratedOrdersAsync(
        AppDbContext db,
        int orderCount,
        CancellationToken cancellationToken)
    {
        var customers = People.Select(Person).ToList();
        var products = Catalog.Select(Product).ToList();
        db.Customers.AddRange(customers);
        db.Products.AddRange(products);
        await db.SaveChangesAsync(cancellationToken);

        // Fixed seed and round-robin customers make a fresh database repeatable.
        // Customer index 0 (Ava Patel) receives orders 0, 12, 24, ... so id 1 is a useful demo row
        // when the database was empty and identity started at 1.
        var random = new Random(20261111);
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var n = 0; n < orderCount; n++)
        {
            var customer = customers[n % customers.Count];
            var itemCount = random.Next(1, 5);
            var items = new List<OrderItem>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                var product = products[random.Next(products.Count)];
                items.Add(Line(product, random.Next(1, 4)));
            }

            var status = n == 0
                ? OrderStatus.Paid
                : (OrderStatus)random.Next(0, 4);

            db.Orders.Add(Order(customer, start.AddHours(n), status, items.ToArray()));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Customer Person(int index)
    {
        var (name, email, city) = People[index];
        return new Customer { Name = name, Email = email, City = city };
    }

    private static Customer Person((string Name, string Email, string City) person) =>
        new() { Name = person.Name, Email = person.Email, City = person.City };

    private static Product Product(int index)
    {
        var (sku, name, price) = Catalog[index];
        return new Product { Sku = sku, Name = name, UnitPrice = price };
    }

    private static Product Product((string Sku, string Name, decimal Price) product) =>
        new() { Sku = product.Sku, Name = product.Name, UnitPrice = product.Price };

    private static OrderItem Line(Product product, int quantity) =>
        new()
        {
            Product = product,
            Quantity = quantity,
            UnitPrice = product.UnitPrice
        };

    private static Order Order(Customer customer, DateTime orderedAtUtc, OrderStatus status, params OrderItem[] items)
    {
        decimal total = 0;
        foreach (var item in items)
            total += item.Quantity * item.UnitPrice;

        return new Order
        {
            Customer = customer,
            OrderedAtUtc = orderedAtUtc,
            Status = status,
            TotalAmount = total,
            Items = [.. items]
        };
    }
}
