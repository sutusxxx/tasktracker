using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskTracker.Api.Tasks;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).HasMaxLength(200);
        builder.Property(t => t.OwnerId).HasMaxLength(64);
        builder.HasIndex(t => new { t.OwnerId, t.CreatedAt });
        builder.Property(t => t.Version).IsRowVersion();
    }
}