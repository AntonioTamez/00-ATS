using TodoApp.Application.Todos;

namespace TodoApp.Api.Todos;

public sealed record CreateTodoRequest(string Title);

/// <summary>Delivery mechanism (interface adapter): HTTP in, use case call, HTTP out.</summary>
public static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/todos");

        group.MapGet("/", async (ListTodos useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(ct)));

        group.MapPost("/", async (CreateTodoRequest body, CreateTodo useCase, CancellationToken ct) =>
        {
            var created = await useCase.ExecuteAsync(new CreateTodoCommand(body.Title), ct);
            return Results.Created($"/api/todos/{created.Id}", created);
        });

        group.MapPost("/{id:guid}/complete", async (Guid id, CompleteTodo useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(id, ct)));

        return app;
    }
}
