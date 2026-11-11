# Benchmark methodology

The benchmark project is [CompiledQueryBenchmarks.cs](../benchmarks/EfCompiledQueries.Benchmarks/CompiledQueryBenchmarks.cs). It uses BenchmarkDotNet against SQL Server. It does not use the EF in-memory provider, and this repository does not publish invented timings.

## How to run

```powershell
dotnet run --project benchmarks/EfCompiledQueries.Benchmarks -c Release
```

Release is required. BenchmarkDotNet rejects a Debug build because the JIT would not match production.

That command runs two classes:

- `SteadyStateBenchmarks` measures lookups, a 20-call loop, a projection, and a graph load against 2,000 orders.
- `DatasetSizeBenchmarks` measures a primary-key lookup and the product-sales aggregate, both normal and compiled, at 200 orders and at 2,000 orders.

BenchmarkDotNet runs each method, and each parameter combination, in its own process. `[GlobalSetup]` reseeds at the start of that process. Seeding is not part of the measured iterations.

First execution is a separate process so the steady-state run cannot warm it:

```powershell
dotnet run --project benchmarks/EfCompiledQueries.Benchmarks -c Release -- --filter *FirstExecution*
```

`FirstExecutionBenchmarks` uses BenchmarkDotNet's cold-start strategy: no warmup, one invocation. That sample includes the first EF translation of that method plus SQL Server and the localhost round trip. It is one observation. It is not a steady-state mean, and it is not a pure measurement of compilation.

## Job settings for the steady-state classes

- Warmup: 3 iterations
- Measurement: 5 iterations
- Invocation count: 1, unroll factor 1, so an iteration is one call (or one 20-call loop, for the repeated methods)
- `[MemoryDiagnoser]`
- Columns include Mean, Error, StdDev, and operations per second
- Server GC is enabled on the benchmark project

A single SQL call on localhost is often only a few milliseconds. BenchmarkDotNet then prints a `MinIterationTime` warning and recommends at least 100ms per iteration. This project keeps the invocation count at 1 so a row stays one request, or one 20-call loop. The warning means the confidence interval is wide. It does not mean the job was rewritten. Read the log before treating a small gap as a decision.

## Data

The benchmark database is `EfCompiledQueriesBench` on `localhost`, Windows authentication, `TrustServerCertificate=True`. The API database is not used.

Customers and products are the fixed fictional catalog. Orders are generated with `Random(20261111)` and assigned round-robin across customers. Both sides of a comparison use the same order id and the same row count.

## What the clock includes

Every steady-state measurement includes:

- EF Core preparing the command
- the localhost round trip
- SQL Server executing the command
- materializing the result used by the benchmark (an id or a count)

The "new context" methods also include creating and disposing an `AppDbContext`, which is what one API request does. The "reused context" methods keep one context for the whole benchmark so context construction is not the dominant cost. `DbContext` stays on one thread.

The clock does not include HTTP, JSON, logging, or middleware. API latency will be higher. `/api/benchmark-info` states the same limits.

## How to read a table

Compare normal and compiled rows that share a name (`Normal_NewContext` with `Compiled_NewContext`, `Heavy_Normal` with `Heavy_Compiled`).

- **Mean** is the end-to-end time, not an EF-only time. Error and StdDev say how stable that mean was on this run.
- **Allocated** is the closest hint of the EF-side difference, because the compiled delegate does not rebuild the expression tree.
- If the heavy aggregate is much slower than the lookup, the database is the larger cost. A compiled delegate cannot remove that aggregate.
- If normal and compiled lookups are close, explicit compilation did not change the request in a way a user would notice. That is a successful measurement, not a failed one.

Raise `WithIterationCount` in `SteadyStateConfig` before treating a small difference as a decision. One laptop, one SQL Server instance, and five iterations are enough to learn the shape of the cost. They are not a capacity plan.

## Limits

- Localhost hides the network cost you would pay for a remote SQL Server. A remote round trip makes the EF lookup an even smaller fraction.
- The product-sales sort of the grouped rows is done in memory. EF Core 10 did not translate that ordered DTO projection inside the compiled delegate. The `GROUP BY` still runs in SQL Server. The sort is a few product names.
- Results are not checked into the README. Copy them from your console if you want a record, and label the machine and the date.
- Do not compare this project with an in-memory provider and call the difference SQL Server performance.
