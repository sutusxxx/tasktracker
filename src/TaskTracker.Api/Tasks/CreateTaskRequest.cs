using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Api.Tasks;

public sealed record CreateTaskRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title);