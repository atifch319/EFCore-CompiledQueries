# Compiled queries guide

This is the mechanism guide. The [README](../README.md) is the map. The runnable queries are in [OrderQueries.cs](../src/EfCompiledQueries.Api/Queries/OrderQueries.cs).

## Three different things

People mix these up. They are not interchangeable.

1. **Normal LINQ.** Your code builds an expression tree every call. EF Core walks that tree when the query executes (`FirstOrDefaultAsync`, `ToListAsync`, `await foreach`, and so on). Building the tree does not hit SQL Server. Executing it does.

2. **The automatic query cache.** EF Core identifies a query by its shape: the operators, with parameter values removed. The first execution of a shape translates LINQ to a command. Later executions of that shape reuse the translation. Changing `id` from 1 to 2 does not create a second cache entry. Adding a `Where`, changing the `Select`, or adding `Include` does.

3. **An explicit compiled query.** `EF.CompileQuery` or `EF.CompileAsyncQuery` produces a delegate. Calling the delegate skips rebuilding the tree and skips the cache lookup. The delegate still sends parameterized SQL on every call. It does not remember the rows.

SQL Server has its own plan cache for the SQL text. That is a fourth cache, and it is not the EF query cache. Parameterized SQL lets SQL Server reuse a plan. A LINQ query that bakes a constant into the tree can defeat both caches. Pass values as arguments.

## What explicit compilation actually saves

Before the cache can be used, EF Core still has to look at the tree you just built and find the matching entry. Microsoft describes that lookup as negligible next to the network and the database, and worth removing only on a measured hot path.

A compiled delegate removes that per-call work. It does not remove:

- the round trip
- SQL Server CPU, IO, or locking
- a bad index
- a projection that loads too much

## Signatures that compile on EF Core 10

Synchronous, one row. The operator inside the expression is `FirstOrDefault`, not `FirstOrDefaultAsync`.

```csharp
public static readonly Func<AppDbContext, int, OrderSummaryDto?> CompiledById =
    EF.CompileQuery((AppDbContext db, int id) =>
        db.Orders
            .Where(order => order.Id == id)
            .Select(order => new OrderSummaryDto(
                order.Id,
                order.Customer.Name,
                order.TotalAmount,
                order.Status))
            .FirstOrDefault());
```

Asynchronous, one row. The cancellation token is a delegate parameter. The body still uses `FirstOrDefault`. EF Core cancels the command with that token.

```csharp
public static readonly Func<AppDbContext, int, CancellationToken, Task<OrderSummaryDto?>> CompiledByIdAsync =
    EF.CompileAsyncQuery((AppDbContext db, int id, CancellationToken cancellationToken) =>
        db.Orders.Where(order => order.Id == id).Select(/* same projection */).FirstOrDefault());
```

Asynchronous, many rows. The expression returns `IQueryable`, and the delegate returns `IAsyncEnumerable`. Read it with `await foreach`. It is not an `IQueryable`, so `ToListAsync` is not available on the delegate.

```csharp
await foreach (var order in OrderQueries.CompiledByCustomer(db, customerId).WithCancellation(cancellationToken))
{
    orders.Add(order);
}
```

`OrderSummaryDto` is a class record so a missing row can be null. A struct would turn "not found" into a default value.

## One model per process

The first context that executes a compiled delegate binds that delegate to its EF model. A later context built with a different model throws:

`The compiled query was executed with a different model than it was compiled against.`

`EnableDetailedErrors`, sensitive-data logging, and a second DI container are enough to create a second model. An application with one `AddDbContext` registration does not hit this. The tests in this repo use a single web host for that reason. Do not cache a `DbContext` in a static field to "match" the delegate. The delegate is static. The context is per request.

## Dynamic shapes

Each branch that adds or removes a `Where` is a different shape. One delegate cannot represent all of them. The automatic cache stores an entry for each shape that actually runs. Forcing one shape with `status == null || order.Status == status` compiles, and it can also produce a worse SQL plan. See [07-FixedVsDynamicQuery.cs](../Examples/07-FixedVsDynamicQuery.cs).

## Projection is a separate decision

`Select` of four columns and `Include` of the order graph answer different questions. Compiling the graph query still loads the graph. See [09-DtoProjectionVsEntityLoading.cs](../Examples/09-DtoProjectionVsEntityLoading.cs).

## Mistakes this repo deliberately avoids

- `FirstOrDefaultAsync` inside `EF.CompileQuery`
- Describing normal LINQ as a full recompile on every call
- Claiming the compiled path skips SQL Server
- A static `DbContext`
- One compiled method per optional filter
- Treating a single `Stopwatch` reading as a result
