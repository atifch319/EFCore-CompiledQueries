# Tradeoffs

Compiled queries trade flexibility and a bit of clarity for a smaller EF Core per-call cost. They do not trade away the database.

## What you gain

- The hot path does not allocate an expression tree or look it up in the query cache on every call.
- The SQL stays parameterized, so SQL Server can reuse its plan.
- The delegate is an ordinary `Func`. There is no repository layer to hide it.
- Async forms exist, so a request thread does not have to block on `EF.CompileQuery`.

## What you give up

- The query shape is fixed when the delegate is created. Optional filters belong in normal LINQ. See [07-FixedVsDynamicQuery.cs](../Examples/07-FixedVsDynamicQuery.cs).
- The delegate is bound to one EF model per process. A second model throws at runtime. Do not toggle `EnableDetailedErrors` or sensitive-data logging on only some contexts in that process.
- `EF.CompileQuery` is synchronous. The API uses `EF.CompileAsyncQuery` so a request does not block a thread on the round trip. The synchronous delegate remains for synchronous callers and for showing the actual `CompileQuery` signature.
- Operators such as `FirstOrDefaultAsync` cannot appear inside the expression. The cancellation token is a parameter of the delegate, and the body uses `FirstOrDefault`.
- Some shapes that translate as normal LINQ do not translate inside a compiled delegate. The product-sales ordering in this repo is one of them. The aggregate stays in SQL; the tiny sort does not.
- Readers must find a static field instead of reading the LINQ at the call site. This project keeps those fields in one file so the cost of that trade is small.

## Suitable

- A stable lookup or list that runs on a large fraction of requests.
- You already measured the normal LINQ version on SQL Server, and the EF portion is visible in the allocations or the mean.
- The query is a projection, not an accident of `Include`.

## Unsuitable

- Admin screens, reports run a few times a day, and anything else whose first-call compilation is noise. The automatic cache covers the second call.
- Search forms whose `Where` clauses come and go.
- Queries whose time is a scan, a hash aggregate, or a missing index.
- A default wrapper around every `DbSet` "for later."

## Decision checklist

Answer these before adding a compiled delegate.

1. Is the shape stable?
2. Is it on a hot path, not an occasional path?
3. Did a SQL Server benchmark, not an in-memory one, show a difference you care about?
4. Have indexes and the projection been looked at first?
5. Can the call stay async on the request thread?
6. Will the process use a single EF model?

If you cannot answer yes to the first four, stay with normal LINQ.

| Scenario | Recommendation |
|---|---|
| Occasional query | Normal LINQ |
| Repeated hot path | Benchmark |
| Highly dynamic query | Normal LINQ |
| Database-heavy query | Optimize SQL first |
| Simple fixed query | Measure |

**Use compiled queries for measured hot paths, not as a default for every query method.**
