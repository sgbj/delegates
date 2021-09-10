# Delegates

New [minimal APIs](https://www.hanselman.com/blog/exploring-a-minimal-web-api-with-aspnet-core-6) were introduced in .NET 6 for mapping endpoints to request delegates.

```csharp
app.MapGet("/api/todos/{id}", async (TodoDbContext db, int id) =>
{
    return await db.Todos.FindAsync(id) is var todo ? Results.Ok(todo) : Results.NotFound();
});
```
Parameters are matched and dependencies are injected using expression trees.

This project is a crude attempt to replicate that functionality.

See [DelegateEndpointRouteBuilderExtensions](https://github.com/dotnet/aspnetcore/blob/main/src/Http/Routing/src/Builder/DelegateEndpointRouteBuilderExtensions.cs) and [RequestDelegateFactory](https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Extensions/src/RequestDelegateFactory.cs).
