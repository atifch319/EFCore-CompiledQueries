using System.Text.Json.Serialization;

namespace EfCompiledQueries.Api.Models;

/// <summary>
/// Stored as text so the database is readable. The JSON converter keeps API
/// responses as names ("Paid") rather than numbers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Shipped = 2,
    Cancelled = 3
}
