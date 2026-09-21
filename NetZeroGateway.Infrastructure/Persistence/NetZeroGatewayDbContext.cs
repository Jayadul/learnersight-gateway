using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Infrastructure.Persistence;

public class NetZeroGatewayDbContext(DbContextOptions<NetZeroGatewayDbContext> options)
    : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ChannelConfig> ChannelConfigs => Set<ChannelConfig>();
    public DbSet<MessageRequest> MessageRequests => Set<MessageRequest>();
    public DbSet<DispatchJob> DispatchJobs => Set<DispatchJob>();
    public DbSet<FallbackPolicy> FallbackPolicies => Set<FallbackPolicy>();
    public DbSet<ApiLog> ApiLogs => Set<ApiLog>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<GatewayCredential> GatewayCredentials => Set<GatewayCredential>();
    public DbSet<DispatchJobRecipient> DispatchJobRecipients => Set<DispatchJobRecipient>();
    public DbSet<ResendAttempt> ResendAttempts => Set<ResendAttempt>();

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

        mb.Entity<ChannelConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ChannelType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Provider).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.ProviderName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.LabelSlug).HasMaxLength(50).IsRequired();
            e.Property(x => x.ConfigJson).HasColumnType("text").IsRequired();
            // Explicit precision: the Npgsql default for decimal is unconstrained
            // numeric, and a provider balance is money — two places, not floating error.
            e.Property(x => x.Balance).HasPrecision(18, 2);
            e.Property(x => x.BalanceCreditLimit).HasPrecision(18, 2);
            e.Property(x => x.BalanceError).HasMaxLength(1000);
            // Unique partial index: exactly one DEFAULT config per channel type. This
            // replaces the old one-active-per-type index — several configs of a type may
            // now be active at once, so uniqueness moved to the default flag.
            e.HasIndex(x => new { x.ChannelType, x.IsDefault })
             .IsUnique()
             .HasFilter("\"is_default\" = true");
            // Labelled sends resolve a slug within a channel type, so it must be unique there.
            e.HasIndex(x => new { x.ChannelType, x.LabelSlug }).IsUnique();
        });

        mb.Entity<MessageRequest>(e =>
        {
            e.HasKey(x => x.Id);
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

        mb.Entity<DispatchJobRecipient>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Recipient).HasMaxLength(50).IsRequired();
            e.Property(x => x.ProviderReferenceId).HasMaxLength(500);
            e.Property(x => x.DeliveryStatus).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.RawStatusResponse).HasColumnType("jsonb");
            // Cascade, unlike the Restrict used elsewhere: these rows are part of the job
            // rather than a separate record that outlives it, so a deleted job should not
            // leave orphaned recipients behind.
            e.HasOne(x => x.DispatchJob)
             .WithMany(x => x.Recipients)
             .HasForeignKey(x => x.DispatchJobId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.DispatchJobId);
            // Status sync looks rows up by the id the provider gave back, and the voice
            // sync — which gets no id — looks them up by number.
            e.HasIndex(x => x.ProviderReferenceId);
            e.HasIndex(x => x.Recipient);
            e.HasIndex(x => x.DeliveryStatus);
            // Historic only since hotfix batch 4 (task 2a reversed the "new row per
            // resend" design) — kept for rows created before this change.
            e.HasIndex(x => x.ResentFromRecipientId);
        });

        mb.Entity<ResendAttempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DeliveryStatus).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.ProviderReferenceId).HasMaxLength(500);
            e.Property(x => x.RawResponse).HasColumnType("jsonb");
            // Cascade: an attempt's history belongs to the recipient row, not a separate
            // record that should outlive it.
            e.HasOne(x => x.DispatchJobRecipient)
             .WithMany()
             .HasForeignKey(x => x.DispatchJobRecipientId)
             .OnDelete(DeleteBehavior.Cascade);
            // Restrict: the dispatch job this attempt used is its own audit record and
            // must not silently vanish if something else ever deletes a job.
            e.HasOne(x => x.DispatchJob)
             .WithMany()
             .HasForeignKey(x => x.DispatchJobId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.DispatchJobRecipientId);
        });

        mb.Entity<FallbackPolicy>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TriggerChannel).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.FallbackChannel).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(500);
            // Restrict, not Cascade: deleting a channel must not silently delete the
            // fallback rules that depend on it — the operator should see the conflict.
            e.HasOne(x => x.TriggerChannelConfig)
             .WithMany()
             .HasForeignKey(x => x.TriggerChannelConfigId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FallbackChannelConfig)
             .WithMany()
             .HasForeignKey(x => x.FallbackChannelConfigId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<ApiLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ToUser).HasMaxLength(2000);
            e.Property(x => x.RawBody).HasColumnType("text").IsRequired();
            e.Property(x => x.ResponseBody).HasColumnType("text").IsRequired();
            e.Property(x => x.CallerIp).HasMaxLength(50).IsRequired();
            // Three channel names at most, so a short bounded column rather than text.
            e.Property(x => x.RequestedChannels).HasMaxLength(100);
            e.HasIndex(x => x.RequestedAt);
            e.HasIndex(x => x.ResponseStatus);
        });

        mb.Entity<GatewayCredential>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            // Ciphertext, not the key itself — AES output is longer than what it wraps.
            e.Property(x => x.ApiKeyEncrypted).HasColumnType("text").IsRequired();
            e.Property(x => x.SecretKeyEncrypted).HasColumnType("text").IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
        });

        mb.Entity<MediaFile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(300).IsRequired();
            e.Property(x => x.StoredFileName).HasMaxLength(300).IsRequired();
            e.Property(x => x.PublicUrl).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Extension).HasMaxLength(20).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            e.Property(x => x.DeletedBy).HasMaxLength(100);
            e.HasIndex(x => x.StoredFileName).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
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
