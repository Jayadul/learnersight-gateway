using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Infrastructure.Persistence;

public class NetZeroGatewayDbContext(DbContextOptions<NetZeroGatewayDbContext> options)
    : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<XSoftApiKey> XSoftApiKeys => Set<XSoftApiKey>();
    public DbSet<XSoftAppConfig> XSoftAppConfigs => Set<XSoftAppConfig>();
    public DbSet<ChannelConfig> ChannelConfigs => Set<ChannelConfig>();
    public DbSet<MessageRequest> MessageRequests => Set<MessageRequest>();
    public DbSet<DispatchJob> DispatchJobs => Set<DispatchJob>();
    public DbSet<FallbackPolicy> FallbackPolicies => Set<FallbackPolicy>();
    public DbSet<ApiLog> ApiLogs => Set<ApiLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<AdminUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(50);
            e.HasIndex(x => x.Username).IsUnique();
        });

        mb.Entity<XSoftApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ApiKeyHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.SecretKeyHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
        });

        mb.Entity<XSoftAppConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.SharedSecretHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
        });

        mb.Entity<ChannelConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ChannelType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.ProviderName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.ConfigJson).HasColumnType("text").IsRequired();
            // Unique partial index: one active config per channel type
            e.HasIndex(x => new { x.ChannelType, x.IsActive })
             .IsUnique()
             .HasFilter("\"is_active\" = true");
        });

        mb.Entity<MessageRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ApiKey).HasMaxLength(200);
            e.Property(x => x.CallerID).HasMaxLength(100);
            e.Property(x => x.ToUser).HasMaxLength(2000).IsRequired();
            e.Property(x => x.RawMessageContent).HasColumnType("text").IsRequired();
            e.Property(x => x.OverallStatus).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.ParsedChannels)
 .HasColumnType("jsonb")
 .HasConversion(
     v => System.Text.Json.JsonSerializer.Serialize(
         v, (System.Text.Json.JsonSerializerOptions?)null),
     v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(
         v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
 .Metadata.SetValueComparer(
     new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
         (a, b) => a != null && b != null && a.SequenceEqual(b),
         v => v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
         v => v.ToList()));
            e.HasMany(x => x.Jobs)
             .WithOne(j => j.MessageRequest)
             .HasForeignKey(j => j.MessageRequestId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ReceivedAt);
            e.HasIndex(x => x.OverallStatus);
        });

        mb.Entity<DispatchJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ChannelType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.MessageContent).HasColumnType("text").IsRequired();
            e.Property(x => x.MediaUrl).HasMaxLength(1000);
            e.Property(x => x.CampaignId).HasMaxLength(200);
            e.Property(x => x.ToNumber).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.ProviderMessageId).HasMaxLength(500);
            e.Property(x => x.ProviderRawResponse).HasColumnType("text");
            e.Property(x => x.FailureReason).HasMaxLength(2000);
            e.HasOne(x => x.ChannelConfig)
             .WithMany()
             .HasForeignKey(x => x.ChannelConfigId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.MessageRequestId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
        });

        mb.Entity<FallbackPolicy>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TriggerChannel).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.FallbackChannel).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(500);
        });

        mb.Entity<ApiLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ApiKey).HasMaxLength(200);
            e.Property(x => x.ToUser).HasMaxLength(2000);
            e.Property(x => x.RawBody).HasColumnType("text").IsRequired();
            e.Property(x => x.ResponseBody).HasColumnType("text").IsRequired();
            e.Property(x => x.CallerIp).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.RequestedAt);
            e.HasIndex(x => x.ResponseStatus);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        return base.SaveChangesAsync(ct);
    }
}
