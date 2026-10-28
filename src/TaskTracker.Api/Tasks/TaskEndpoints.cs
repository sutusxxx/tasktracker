using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Auth;
using TaskTracker.Api.Data;

namespace TaskTracker.Api.Tasks;

internal static class TaskEndpoints
{
    private static readonly Expression<Func<TaskItem, TaskResponse>> ToResponse =
        t => new TaskResponse(t.Id, t.Title, t.OwnerId, t.Done, t.CreatedAt);

    public static void MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tasks");

        group.MapGet("/", async ([AsParameters] TaskQuery query, AppDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var filtered = db.Tasks.AsNoTracking()
                .Where(t => t.OwnerId == userId)
                .ApplyFilters(query);

            var total = await filtered.CountAsync(ct);
            var items = await filtered
                .ApplySort(query)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToResponse)
                .ToArrayAsync(ct);

            return new PagedResponse<TaskResponse>(items, query.Page, query.PageSize, total);
        });

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            return await db.Tasks.AsNoTracking()
                .Where(t => t.Id == id && t.OwnerId == userId)
                .Select(ToResponse)
                .FirstOrDefaultAsync(ct) is { } task
                ? Results.Ok(task)
                : Results.NotFound();
        });

        group.MapPost("/", async (CreateTaskRequest request, AppDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var task = new TaskItem
            {
                Title = request.Title,
                OwnerId = user.GetUserId()
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/tasks/{task.Id}",
                new TaskResponse(task.Id, task.Title, task.OwnerId, task.Done, task.CreatedAt));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTaskRequest request, AppDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId, ct);
            if (task is null) return Results.NotFound();

            task.Title = request.Title;
            task.Done = request.Done;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            return await db.Tasks.Where(t => t.Id == id && t.OwnerId == userId).ExecuteDeleteAsync(ct) == 1
                ? Results.NoContent()
                : Results.NotFound();
        });

        var adminGroup = app.MapGroup("/admin/tasks").RequireAuthorization(Policies.Admin);

        adminGroup.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            await db.Tasks.AsNoTracking()
                .OrderBy(t => t.CreatedAt)
                .Select(ToResponse)
                .ToArrayAsync(ct));
    }
}