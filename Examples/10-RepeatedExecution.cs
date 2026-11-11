namespace EfCompiledQueries.Examples;

/// <summary>
/// Runs the same shape many times on one context, sequentially.
/// DbContext is not thread-safe; the loop stays on one thread.
/// The first normal execution fills the EF query cache. Later iterations reuse it.
/// The compiled delegate does not rebuild the tree. Both still query SQL Server every time.
/// This method checks that the results stay stable. It is not a performance measurement.
/// </summary>
public static class RepeatedExecution
{
    public const string Takeaway =
        "Repeat a query to judge steady-state behavior, and keep the first execution separate from the warmed-up calls. Repetition does not cache the rows.";

    public static async Task<bool> ResultsStayStableAsync(
        AppDbContext db,
        int id,
        int times,
        CancellationToken cancellationToken)
    {
        if (times < 1)
            throw new ArgumentOutOfRangeException(nameof(times));

        OrderSummaryDto? first = null;
        for (var i = 0; i < times; i++)
        {
            var normal = await OrderQueries.NormalByIdAsync(db, id, cancellationToken);
            var compiled = await OrderQueries.CompiledByIdAsync(db, id, cancellationToken);
            if (normal != compiled)
                return false;

            if (i == 0)
                first = normal;
            else if (normal != first)
                return false;
        }

        return first is not null;
    }
}
