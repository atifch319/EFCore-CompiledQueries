namespace EfCompiledQueries.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class OrderQueryTests(SqlServerFixture database)
{
    [Fact]
    public async Task Normal_and_compiled_lookups_return_the_same_order()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var comparison = await NormalVsCompiled.CompareAsync(db, database.PaidOrderId, CancellationToken.None);

        Assert.True(comparison.AllMatch);
        Assert.Equal(database.PaidOrder, comparison.Normal);
    }

    [Fact]
    public async Task Missing_order_is_null_for_normal_and_both_compiled_forms()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var comparison = await NormalVsCompiled.CompareAsync(db, SqlServerFixture.MissingId, CancellationToken.None);

        Assert.True(comparison.AllMatch);
        Assert.Null(comparison.Normal);
    }

    [Fact]
    public async Task Projection_contains_the_seeded_customer_total_and_status()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var order = await NormalEfCoreQuery.ByIdAsync(db, database.PaidOrderId, CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal("Ava Patel", order.CustomerName);
        Assert.Equal(41.00m, order.TotalAmount);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task Async_compiled_customer_query_matches_the_normal_list()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var normal = await OrderQueries.NormalByCustomerAsync(db, database.AvaCustomerId, CancellationToken.None);
        var compiled = await OrderQueries.CompiledByCustomerAsync(db, database.AvaCustomerId, CancellationToken.None);
        var enumerated = await AsyncCompiledQuery.ByCustomerAsync(db, database.AvaCustomerId, CancellationToken.None);

        Assert.NotNull(normal);
        Assert.Equal(database.AvaOrderCount, normal.Count);
        Assert.Equal(normal, compiled);
        Assert.Equal(normal, enumerated);
        Assert.All(normal, order => Assert.Equal("Ava Patel", order.CustomerName));
    }

    [Fact]
    public async Task Repeated_execution_stays_stable_on_one_context()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var stable = await RepeatedExecution.ResultsStayStableAsync(
            db,
            database.PaidOrderId,
            times: 8,
            CancellationToken.None);

        Assert.True(stable);
    }

    [Fact]
    public async Task Customer_filter_returns_only_that_customers_orders()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var ava = await OrderQueries.NormalByCustomerAsync(db, database.AvaCustomerId, CancellationToken.None);
        var leo = await OrderQueries.NormalByCustomerAsync(db, database.LeoCustomerId, CancellationToken.None);
        var missing = await OrderQueries.CompiledByCustomerAsync(db, SqlServerFixture.MissingId, CancellationToken.None);

        Assert.NotNull(ava);
        Assert.Equal(2, ava.Count);
        Assert.NotNull(leo);
        Assert.Empty(leo);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Same_query_shape_accepts_two_ids_and_compiled_matches()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var result = await EfCoreQueryCache.RunAsync(
            db,
            database.PaidOrderId,
            database.SecondOrderId,
            CancellationToken.None);

        Assert.NotNull(result.FirstId);
        Assert.NotNull(result.SecondId);
        Assert.NotEqual(result.FirstId.Id, result.SecondId.Id);
        Assert.True(result.CompiledMatchesFirst);
    }

    [Fact]
    public async Task Product_sales_normal_and_compiled_match_the_hand_checked_totals()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var normal = await OrderQueries.ProductSalesNormalAsync(db, CancellationToken.None);
        var compiled = await OrderQueries.ProductSalesCompiledAsync(db, CancellationToken.None);
        var expected = new[]
        {
            new ProductSalesDto("Northwind Coffee Beans", 4, 64.00m),
            new ProductSalesDto("Cedar Desk Lamp", 2, 48.00m),
            new ProductSalesDto("Field Notes Notebook", 2, 17.00m)
        };

        Assert.Equal(expected, normal);
        Assert.Equal(expected, compiled);
    }

    [Fact]
    public async Task Graph_load_matches_the_projected_customer_and_line_total()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var comparison = await DtoProjectionVsEntityLoading.CompareAsync(
            db,
            database.PaidOrderId,
            CancellationToken.None);

        Assert.True(comparison.CustomerNameMatches);
        Assert.Equal(2, comparison.GraphItemCount);
        Assert.Equal(41.00m, comparison.Projected!.TotalAmount);
    }

    [Fact]
    public async Task Dynamic_search_changes_rows_when_the_shape_changes()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var paidForAva = await FixedVsDynamicQuery.DynamicSearchAsync(
            db,
            new OrderSearch(database.AvaCustomerId, OrderStatus.Paid, null, null),
            CancellationToken.None);
        var fromMay = await FixedVsDynamicQuery.DynamicSearchAsync(
            db,
            new OrderSearch(null, null, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), null),
            CancellationToken.None);
        var everyone = await FixedVsDynamicQuery.DynamicSearchAsync(
            db,
            new OrderSearch(null, null, null, null),
            CancellationToken.None);

        Assert.Single(paidForAva);
        Assert.Equal(41.00m, paidForAva[0].TotalAmount);
        Assert.Equal(2, fromMay.Count);
        Assert.Equal(4, everyone.Count);
    }

    [Fact]
    public async Task Cold_path_returns_cancelled_orders_for_the_city_only()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var result = await HotPathVsColdPath.RunAsync(
            db,
            database.PaidOrderId,
            SqlServerFixture.ColdPathCity,
            CancellationToken.None);
        var seattle = await HotPathVsColdPath.CancelledOrdersInCityAsync(db, "Seattle", CancellationToken.None);

        Assert.Equal(database.PaidOrder, result.HotPathOrder);
        var cancelled = Assert.Single(result.ColdPathOrders);
        Assert.Equal("Maya Chen", cancelled.CustomerName);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Empty(seattle);
    }

    [Fact]
    public async Task Service_stays_aligned_with_the_query_methods()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var service = new OrderService(db, NullLogger<OrderService>.Instance);

        var normal = await service.GetOrderNormalAsync(database.PaidOrderId, CancellationToken.None);
        var compiled = await service.GetOrderCompiledAsync(database.PaidOrderId, CancellationToken.None);
        var customer = await service.GetCustomerOrdersCompiledAsync(database.AvaCustomerId, CancellationToken.None);
        var direct = await OrderQueries.NormalByCustomerAsync(db, database.AvaCustomerId, CancellationToken.None);

        Assert.Equal(database.PaidOrder, normal);
        Assert.Equal(normal, compiled);
        Assert.Equal(direct, customer);
    }

    [Fact]
    public void Decision_guide_sends_hot_paths_to_measurement_and_dynamic_queries_to_linq()
    {
        var rows = PracticalDecisionGuide.All();
        var hot = PracticalDecisionGuide.Recommend(QueryScenario.RepeatedHotPath);
        var dynamic = PracticalDecisionGuide.Recommend(QueryScenario.HighlyDynamic);

        Assert.Equal(5, rows.Count);
        Assert.Equal("Benchmark", hot.Recommendation);
        Assert.Equal("Normal LINQ", dynamic.Recommendation);
        Assert.DoesNotContain(rows, row => row.Recommendation.Contains("always", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Database_cost_example_returns_one_order_and_the_full_aggregate()
    {
        await using var scope = database.CreateScope();
        var db = scope.Db;
        var result = await DatabaseCostVsEfOverhead.RunAsync(db, database.PaidOrderId, CancellationToken.None);

        Assert.Equal(database.PaidOrder, result.SingleOrder);
        Assert.Equal(3, result.ProductSales.Count);
    }
}
