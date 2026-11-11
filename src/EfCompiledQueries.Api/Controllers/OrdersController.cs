using EfCompiledQueries.Api.Dtos;
using EfCompiledQueries.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EfCompiledQueries.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    /// <summary>
    /// Looks up one order with normal LINQ. EF Core caches that query shape after the first execution.
    /// </summary>
    [HttpGet("orders/{id:int}/normal")]
    [Tags("Orders")]
    [ProducesResponseType(typeof(OrderSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderSummaryDto>> GetNormal(int id, CancellationToken cancellationToken)
    {
        var order = await orders.GetOrderNormalAsync(id, cancellationToken);
        return order is null ? NotFoundProblem("Order", id) : Ok(order);
    }

    /// <summary>
    /// Looks up one order with an explicit compiled query. SQL Server still executes the command.
    /// </summary>
    [HttpGet("orders/{id:int}/compiled")]
    [Tags("Orders")]
    [ProducesResponseType(typeof(OrderSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderSummaryDto>> GetCompiled(int id, CancellationToken cancellationToken)
    {
        var order = await orders.GetOrderCompiledAsync(id, cancellationToken);
        return order is null ? NotFoundProblem("Order", id) : Ok(order);
    }

    /// <summary>Lists one customer's orders with normal LINQ. An unknown customer is 404. A customer with no orders is an empty array.</summary>
    [HttpGet("customers/{customerId:int}/orders/normal")]
    [Tags("Customers")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetCustomerOrdersNormal(
        int customerId,
        CancellationToken cancellationToken)
    {
        var result = await orders.GetCustomerOrdersNormalAsync(customerId, cancellationToken);
        return result is null ? NotFoundProblem("Customer", customerId) : Ok(result);
    }

    /// <summary>Lists one customer's orders with an async compiled query. Results are read with await foreach inside OrderQueries.</summary>
    [HttpGet("customers/{customerId:int}/orders/compiled")]
    [Tags("Customers")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetCustomerOrdersCompiled(
        int customerId,
        CancellationToken cancellationToken)
    {
        var result = await orders.GetCustomerOrdersCompiledAsync(customerId, cancellationToken);
        return result is null ? NotFoundProblem("Customer", customerId) : Ok(result);
    }

    /// <summary>
    /// Describes what the benchmark project measures. It does not return timing numbers.
    /// </summary>
    [HttpGet("benchmark-info")]
    [Tags("Benchmarks")]
    [ProducesResponseType(typeof(BenchmarkInfoDto), StatusCodes.Status200OK)]
    public ActionResult<BenchmarkInfoDto> GetBenchmarkInfo() => Ok(orders.GetBenchmarkInfo());

    private ObjectResult NotFoundProblem(string resource, int id) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: $"{resource} not found",
            detail: $"No {resource.ToLowerInvariant()} exists with id {id}.");
}
