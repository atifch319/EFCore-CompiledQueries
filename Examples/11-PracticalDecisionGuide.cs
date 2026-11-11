namespace EfCompiledQueries.Examples;

/// <summary>
/// The same decision table as the README, in code, so a caller can ask for one row.
/// "Benchmark" means measure the hot path. It does not mean the compiled query has already won.
/// </summary>
public static class PracticalDecisionGuide
{
    public const string Takeaway =
        "Use compiled queries for measured hot paths, not as a default for every query method.";

    public static QueryRecommendation Recommend(QueryScenario scenario) => scenario switch
    {
        QueryScenario.Occasional => new(
            scenario,
            "Usually sufficient",
            "Possible",
            "Low",
            "Normal LINQ"),
        QueryScenario.RepeatedHotPath => new(
            scenario,
            "Good baseline",
            "Worth measuring",
            "Potentially useful",
            "Benchmark"),
        QueryScenario.HighlyDynamic => new(
            scenario,
            "Flexible",
            "Less suitable",
            "Uncertain",
            "Normal LINQ"),
        QueryScenario.DatabaseHeavy => new(
            scenario,
            "Often sufficient",
            "May help slightly",
            "Often limited",
            "Optimize SQL first"),
        QueryScenario.SimpleFixed => new(
            scenario,
            "Good",
            "Good candidate",
            "Depends on workload",
            "Measure"),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario.")
    };

    public static IReadOnlyList<QueryRecommendation> All() =>
        Enum.GetValues<QueryScenario>().Select(Recommend).ToArray();
}

public enum QueryScenario
{
    Occasional,
    RepeatedHotPath,
    HighlyDynamic,
    DatabaseHeavy,
    SimpleFixed
}

public sealed record QueryRecommendation(
    QueryScenario Scenario,
    string NormalQuery,
    string CompiledQuery,
    string LikelyBenefit,
    string Recommendation);
