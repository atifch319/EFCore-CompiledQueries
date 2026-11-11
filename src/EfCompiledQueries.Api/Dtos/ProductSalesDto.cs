namespace EfCompiledQueries.Api.Dtos;

/// <summary>
/// Aggregate row for the database-heavy example. The database computes the sums.
/// </summary>
public sealed record ProductSalesDto(string ProductName, int UnitsSold, decimal Revenue);
