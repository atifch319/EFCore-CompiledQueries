namespace EfCompiledQueries.Api.Dtos;

public sealed record BenchmarkInfoDto(
    string Lesson,
    bool MeasuredTimeIncludesSqlServerExecution,
    bool MeasuredTimeIncludesNetworkRoundTrip,
    bool MeasuredTimeIncludesHttpPipeline,
    string HowToRun,
    IReadOnlyList<string> ComparedApproaches,
    string Interpretation);
