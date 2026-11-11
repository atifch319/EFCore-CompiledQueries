using System.Reflection;
using System.Text.Json;
using EfCompiledQueries.Api.Data;
using EfCompiledQueries.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Orders");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Orders' is missing. See README.md for the local SQL Server setup.");
}

// JSON console keeps the structured placeholders (OrderId, QueryMode) as fields.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "EF Core Compiled Queries",
        Version = "v1",
        Description = "Normal LINQ and explicit compiled queries for an order lookup. Compiled queries still execute SQL on SQL Server."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Keep these options stable. A compiled delegate is bound to the model from
    // the first context that runs it. EnableDetailedErrors and sensitive-data
    // logging change that model and must not be toggled per request.
    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<OrderService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
    var profileName = app.Configuration["Seed:Profile"] ?? nameof(SeedProfile.Development);
    if (!Enum.TryParse<SeedProfile>(profileName, ignoreCase: true, out var profile))
    {
        throw new InvalidOperationException(
            $"Seed:Profile '{profileName}' is invalid. Use Tests or Development.");
    }

    try
    {
        await DatabaseSeeder.EnsureCreatedAndSeedAsync(db, profile, logger);
    }
    catch (Exception exception)
    {
        logger.LogCritical(
            exception,
            "Could not open SQL Server. Check ConnectionStrings:Orders and that the local instance is running.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Order API");
    options.DocumentTitle = "EF Core Compiled Queries";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();
app.Run();

public partial class Program;
