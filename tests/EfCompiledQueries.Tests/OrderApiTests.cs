using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EfCompiledQueries.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class OrderApiTests(SqlServerFixture database)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Normal_endpoint_returns_the_seeded_order()
    {
        var order = await GetOrderAsync($"/api/orders/{database.PaidOrderId}/normal");
        Assert.Equal(database.PaidOrder, order);
    }

    [Fact]
    public async Task Compiled_endpoint_returns_the_same_json_as_the_normal_endpoint()
    {
        using var client = database.CreateClient();
        var normalResponse = await client.GetAsync($"/api/orders/{database.PaidOrderId}/normal");
        var normal = await normalResponse.Content.ReadAsStringAsync();
        Assert.True(normalResponse.IsSuccessStatusCode, normal);

        var compiledResponse = await client.GetAsync($"/api/orders/{database.PaidOrderId}/compiled");
        var compiled = await compiledResponse.Content.ReadAsStringAsync();
        Assert.True(compiledResponse.IsSuccessStatusCode, compiled);
        Assert.Equal(normal, compiled);
    }

    [Fact]
    public async Task Missing_order_is_not_found()
    {
        using var client = database.CreateClient();
        var response = await client.GetAsync($"/api/orders/{SqlServerFixture.MissingId}/compiled");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.NotFound, body);
        Assert.Contains("Order not found", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Customer_endpoints_match_and_reject_an_unknown_customer()
    {
        using var client = database.CreateClient();
        var normalResponse = await client.GetAsync($"/api/customers/{database.AvaCustomerId}/orders/normal");
        var normal = await normalResponse.Content.ReadAsStringAsync();
        Assert.True(normalResponse.IsSuccessStatusCode, normal);

        var compiledResponse = await client.GetAsync($"/api/customers/{database.AvaCustomerId}/orders/compiled");
        var compiled = await compiledResponse.Content.ReadAsStringAsync();
        Assert.True(compiledResponse.IsSuccessStatusCode, compiled);
        var missing = await client.GetAsync($"/api/customers/{SqlServerFixture.MissingId}/orders/normal");
        var empty = await client.GetFromJsonAsync<List<OrderSummaryDto>>(
            $"/api/customers/{database.LeoCustomerId}/orders/compiled",
            JsonOptions);

        Assert.Equal(normal, compiled);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.NotNull(empty);
        Assert.Empty(empty);
    }

    [Fact]
    public async Task Benchmark_info_describes_the_measurement_and_does_not_invent_numbers()
    {
        using var client = database.CreateClient();
        var info = await client.GetFromJsonAsync<BenchmarkInfoDto>("/api/benchmark-info", JsonOptions);

        Assert.NotNull(info);
        Assert.True(info.MeasuredTimeIncludesSqlServerExecution);
        Assert.True(info.MeasuredTimeIncludesNetworkRoundTrip);
        Assert.False(info.MeasuredTimeIncludesHttpPipeline);
        Assert.Contains("Compiled queries optimize EF overhead", info.Lesson, StringComparison.Ordinal);
        Assert.DoesNotContain(" ms", info.Interpretation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swagger_document_lists_the_demo_paths()
    {
        using var client = database.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/orders/{id}/normal", body, StringComparison.Ordinal);
        Assert.Contains("/api/orders/{id}/compiled", body, StringComparison.Ordinal);
        Assert.Contains("/api/customers/{customerId}/orders/normal", body, StringComparison.Ordinal);
        Assert.Contains("/api/customers/{customerId}/orders/compiled", body, StringComparison.Ordinal);
        Assert.Contains("/api/benchmark-info", body, StringComparison.Ordinal);
    }

    private async Task<OrderSummaryDto> GetOrderAsync(string url)
    {
        using var client = database.CreateClient();
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var order = JsonSerializer.Deserialize<OrderSummaryDto>(body, JsonOptions);
        Assert.NotNull(order);
        return order;
    }
}
