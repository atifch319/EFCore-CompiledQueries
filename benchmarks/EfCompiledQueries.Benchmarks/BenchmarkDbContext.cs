using EfCompiledQueries.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EfCompiledQueries.Benchmarks;

/// <summary>
/// Benchmark session for the one EF model in AppDbContext.
/// This is not a second DbContext: compiled queries are bound to AppDbContext,
/// so benchmarks must call that same type.
/// </summary>
public static class BenchmarkDbContext
{
    public const int SteadyStateOrderCount = 2000;

    public static int SampleOrderId { get; private set; }

    private static DbContextOptions<AppDbContext>? _options;

    public static async Task<bool> ServerIsReachableAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(LocalSqlConnection.ForDatabase("master"))
                .Options;
            await using var db = new AppDbContext(options);
            return await db.Database.CanConnectAsync();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return false;
        }
    }

    public static async Task ResetAsync(int orderCount, CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(LocalSqlConnection.ForDatabase(LocalSqlConnection.BenchmarkDatabase))
            .Options;

        await using (var db = new AppDbContext(options))
        {
            // The benchmark database is recreated so row counts and the schema match this build.
            // The API database is left alone.
            await db.Database.EnsureDeletedAsync(cancellationToken);
            await DatabaseSeeder.EnsureCreatedAndSeedAsync(
                db,
                SeedProfile.Benchmark,
                NullLogger.Instance,
                orderCount,
                cancellationToken);

            SampleOrderId = await db.Orders
                .OrderBy(order => order.Id)
                .Select(order => order.Id)
                .FirstAsync(cancellationToken);
        }

        _options = options;
    }

    public static AppDbContext Create()
    {
        if (_options is null)
            throw new InvalidOperationException("Call ResetAsync before creating a benchmark context.");

        return new AppDbContext(_options);
    }
}
