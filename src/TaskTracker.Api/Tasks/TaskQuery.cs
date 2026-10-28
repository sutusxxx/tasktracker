using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Api.Tasks;

public enum TaskSortField { CreatedAt, Title }

public sealed record TaskQuery(
    [property: Range(1, 100_000)] int Page = 1,
    [property: Range(1, 100)] int PageSize = 20,
    string? Search = null,
    bool? Done = null,
    TaskSortField Sort = TaskSortField.CreatedAt,
    bool Desc = false);