using Microsoft.EntityFrameworkCore;

namespace TaskTracker.Api.Tasks;

internal static class TaskQueryExtensions
{
    public static IQueryable<TaskItem> ApplyFilters(this IQueryable<TaskItem> source, TaskQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            source = source.Where(t => EF.Functions.ILike(t.Title, pattern, "\\"));
        }

        if (query.Done is { } done)
        {
            source = source.Where(t => t.Done == done);
        }

        return source;
    }

    public static IQueryable<TaskItem> ApplySort(this IQueryable<TaskItem> source, TaskQuery query)
    {
        var ordered = (query.Sort, query.Desc) switch
        {
            (TaskSortField.Title, false) => source.OrderBy(t => t.Title),
            (TaskSortField.Title, true) => source.OrderByDescending(t => t.Title),
            (_, false) => source.OrderBy(t => t.CreatedAt),
            (_, true) => source.OrderByDescending(t => t.CreatedAt),
        };

        return query.Desc ? ordered.ThenByDescending(t => t.Id) : ordered.ThenBy(t => t.Id);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}