using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Api.Tasks;

public sealed record UpdateTaskRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title, 
    bool Done,
    uint Version
    );