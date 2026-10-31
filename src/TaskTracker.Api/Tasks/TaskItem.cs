namespace TaskTracker.Api.Tasks;

internal sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Title { get; set; }
    public required string OwnerId { get; set; }
    public bool Done { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public uint Version  { get; set; }
}