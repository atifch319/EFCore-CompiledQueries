using EfCompiledQueries.Api.Models;

namespace EfCompiledQueries.Api.Dtos;

/// <summary>
/// Class record, not a struct, so a missing order can be represented as null.
/// A struct would make FirstOrDefault return a default value instead.
/// </summary>
public sealed record OrderSummaryDto(
    int Id,
    string CustomerName,
    decimal TotalAmount,
    OrderStatus Status);
