using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EfCompiledQueries.Tests;

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

/// <summary>
/// One web host for every test. Compiled queries bind to the first EF model that
/// executes them, so the suite must not also spin up a second AppDbContext provider.
/// The database is SQL Server. These tests check behavior, not latency.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;

    public string ConnectionString { get; } = LocalSqlConnection.ForDatabase(LocalSqlConnection.TestDatabase);

    public int PaidOrderId { get; private set; }

    public int SecondOrderId { get; private set; }

    public int AvaCustomerId { get; private set; }

    public int LeoCustomerId { get; private set; }

    public OrderSummaryDto PaidOrder { get; private set; } = null!;

    public int AvaOrderCount { get; private set; }

    public const int MissingId = 999999;

    public const string ColdPathCity = "Denver";

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        // Delete only. Do not run a compiled query on this short-lived context.
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Orders"] = ConnectionString,
                    ["Seed:Profile"] = nameof(SeedProfile.Tests)
                });
            });

            // Program reads the connection string while the host is built, which can
            // happen before the in-memory values above win. Replace the context so the
            // test database cannot fall through to the development database.
            builder.ConfigureServices(services =>
            {
                var registrations = services
                    .Where(service =>
                        service.ServiceType == typeof(DbContextOptions<AppDbContext>)
                        || service.ServiceType == typeof(AppDbContext))
                    .ToList();

                foreach (var registration in registrations)
                    services.Remove(registration);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(ConnectionString));
            });
        });

        // Building the host runs Program's seeder against the empty test database.
        using var client = _factory.CreateClient();
        _ = client;

        await using var scope = CreateScope();
        var db = scope.Db;
        var ava = await db.Customers.SingleAsync(customer => customer.Email == "ava.patel@example.test");
        var leo = await db.Customers.SingleAsync(customer => customer.Email == "leo.martins@example.test");
        var paid = await db.Orders.SingleAsync(order => order.CustomerId == ava.Id && order.Status == OrderStatus.Paid);
        var shipped = await db.Orders.SingleAsync(order => order.CustomerId == ava.Id && order.Status == OrderStatus.Shipped);

        AvaCustomerId = ava.Id;
        LeoCustomerId = leo.Id;
        PaidOrderId = paid.Id;
        SecondOrderId = shipped.Id;
        AvaOrderCount = 2;
        PaidOrder = new OrderSummaryDto(paid.Id, ava.Name, paid.TotalAmount, paid.Status);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    public HttpClient CreateClient() => _factory.CreateClient();

    public AppScope CreateScope() => new(_factory.Services.CreateScope());
}

public sealed class AppScope : IAsyncDisposable
{
    private readonly IServiceScope _scope;

    public AppScope(IServiceScope scope)
    {
        _scope = scope;
        Db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public AppDbContext Db { get; }

    public ValueTask DisposeAsync()
    {
        _scope.Dispose();
        return ValueTask.CompletedTask;
    }
}
