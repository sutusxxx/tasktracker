namespace TaskTracker.Api.Tasks;

internal sealed record TaskResponse(Guid Id, string Title, string OwnerId, bool Done, DateTimeOffset CreatedAt);