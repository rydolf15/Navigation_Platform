using Microsoft.EntityFrameworkCore;

namespace NavigationPlatform.Gateway.Persistence;

public sealed class GatewayDbContext : DbContext
{
    public DbSet<UserAccountStatus> UserAccountStatuses => Set<UserAccountStatus>();
    public DbSet<UserStatusAudit> UserStatusAudits => Set<UserStatusAudit>();
    public DbSet<GatewayOutboxMessage> OutboxMessages => Set<GatewayOutboxMessage>();

    public GatewayDbContext(DbContextOptions<GatewayDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccountStatus>(b =>
        {
            b.ToTable("user_account_statuses");
            b.HasKey(x => x.UserId);

            b.Property(x => x.UserId).HasColumnName("user_id");
            b.Property(x => x.Status).HasColumnName("status").IsRequired();
            b.Property(x => x.UpdatedUtc).HasColumnName("updated_utc").IsRequired();
        });

        modelBuilder.Entity<UserStatusAudit>(b =>
        {
            b.ToTable("user_status_audits");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            b.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
            b.Property(x => x.OldStatus).HasColumnName("old_status");
            b.Property(x => x.NewStatus).HasColumnName("new_status").IsRequired();
            b.Property(x => x.OccurredUtc).HasColumnName("occurred_utc").IsRequired();

            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.OccurredUtc);
        });

        modelBuilder.Entity<GatewayOutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Type).HasColumnName("type").IsRequired();
            b.Property(x => x.Payload).HasColumnName("payload").IsRequired();
            b.Property(x => x.OccurredUtc).HasColumnName("occurred_utc").IsRequired();
            b.Property(x => x.Processed).HasColumnName("processed").IsRequired();

            b.HasIndex(x => x.Processed);
        });
    }
}

