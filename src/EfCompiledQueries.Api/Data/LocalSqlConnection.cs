namespace EfCompiledQueries.Api.Data;

/// <summary>
/// Local Windows-authentication connection strings. No password is stored here.
/// The API itself reads ConnectionStrings:Orders from configuration so a machine
/// can override the server without changing code.
/// </summary>
public static class LocalSqlConnection
{
    public const string AppDatabase = "EfCompiledQueries";

    public const string TestDatabase = "EfCompiledQueriesTests";

    public const string BenchmarkDatabase = "EfCompiledQueriesBench";

    public static string ForDatabase(string databaseName) =>
        $"Server=localhost;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";
}
