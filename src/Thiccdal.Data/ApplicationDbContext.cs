using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;

namespace Thiccdal.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BotCommand> BotCommands { get; set; }

    public DbSet<ChecklistSession> ChecklistSessions { get; set; }

    public DbSet<ChecklistSessionItem> ChecklistSessionItems { get; set; }

    public DbSet<CustomChecklistItem> CustomChecklistItems { get; set; }

    public DbSet<TwitchToken> TwitchTokens { get; set; }

    public DbSet<TwitchTargetChannelConfiguration> TwitchTargetChannels { get; set; }


    public DbSet<PlatformEvent> PlatformEvents { get; set; }

    public DbSet<UserIdentity> UserIdentities { get; set; }

    public DbSet<PlatformUser> PlatformUsers { get; set; }

    public DbSet<UserIdentitySuggestion> UserIdentitySuggestions { get; set; }

    public DbSet<ChatMessage> ChatMessages { get; set; }

    public DbSet<ChatterMemoryReset> ChatterMemoryResets { get; set; }

    public DbSet<ProactiveMessage> ProactiveMessages { get; set; }

    public DbSet<AppConfiguration> AppConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BotCommand>(botCommand =>
        {
            botCommand.Property(command => command.Trigger)
                .IsRequired();

            botCommand.Property(command => command.ResponseTemplate)
                .IsRequired();

            botCommand.HasIndex(command => command.Trigger)
                .IsUnique();
        });

        modelBuilder.Entity<ChecklistSession>(checklistSession =>
        {
            checklistSession.Property(session => session.SessionId)
                .IsRequired();

            checklistSession.Property(session => session.RecordedAt)
                .IsRequired();

            checklistSession.HasIndex(session => session.SessionId)
                .IsUnique();

            checklistSession.HasMany(session => session.Items)
                .WithOne(item => item.ChecklistSession)
                .HasForeignKey(item => item.ChecklistSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChecklistSessionItem>(checklistSessionItem =>
        {
            checklistSessionItem.Property(item => item.ItemId)
                .IsRequired();

            checklistSessionItem.Property(item => item.Category)
                .IsRequired();

            checklistSessionItem.Property(item => item.Label)
                .IsRequired();

            checklistSessionItem.Property(item => item.Status)
                .IsRequired();

            checklistSessionItem.HasIndex(item => new
            {
                item.ChecklistSessionId,
                item.ItemId
            }).IsUnique();
        });

        modelBuilder.Entity<CustomChecklistItem>(customChecklistItem =>
        {
            customChecklistItem.Property(item => item.Label)
                .IsRequired();

            customChecklistItem.Property(item => item.DisplayOrder)
                .IsRequired();

            customChecklistItem.Property(item => item.IsEnabled)
                .IsRequired();
        });

        modelBuilder.Entity<ProactiveMessage>(proactiveMessage =>
        {
            proactiveMessage.Property(message => message.Message)
                .IsRequired();

            proactiveMessage.Property(message => message.IntervalSeconds)
                .IsRequired();
        });

        modelBuilder.Entity<TwitchTargetChannelConfiguration>()
            .Property(configuration => configuration.TargetChannel)
            .IsRequired();

        modelBuilder.Entity<PlatformEvent>(platformEvent =>
        {
            platformEvent.Property(eventRecord => eventRecord.Source)
                .HasConversion<string>()
                .HasColumnName("Platform")
                .IsRequired();

            platformEvent.Property(eventRecord => eventRecord.Author)
                .IsRequired();

            platformEvent.Property(eventRecord => eventRecord.Channel)
                .IsRequired();

            platformEvent.Property(eventRecord => eventRecord.Summary)
                .IsRequired();

            platformEvent.Property(eventRecord => eventRecord.ExternalId)
                .IsRequired();

            platformEvent.Property(eventRecord => eventRecord.SourceEventType)
                .IsRequired();

            platformEvent.Property(eventRecord => eventRecord.Content)
                .IsRequired();

            platformEvent.Property(eventRecord => eventRecord.HtmlContent)
                .IsRequired();

            platformEvent.Property(eventRecord => eventRecord.RawData)
                .IsRequired();

            platformEvent.HasDiscriminator<string>("EventType")
                .HasValue<PlatformEvent>(nameof(PlatformEvent))
                .HasValue<SubscribeEvent>(nameof(SubscribeEvent))
                .HasValue<FollowEvent>(nameof(FollowEvent))
                .HasValue<RedeemEvent>(nameof(RedeemEvent))
                .HasValue<RaidEvent>(nameof(RaidEvent));
        });

        modelBuilder.Entity<SubscribeEvent>()
            .Property(subscribeEvent => subscribeEvent.Tier)
            .IsRequired();

        modelBuilder.Entity<SubscribeEvent>()
            .HasOne(subscribeEvent => subscribeEvent.GifterPlatformUser)
            .WithMany()
            .HasForeignKey(subscribeEvent => subscribeEvent.GifterPlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RedeemEvent>()
            .Property(redeemEvent => redeemEvent.RewardId)
            .IsRequired();

        modelBuilder.Entity<RedeemEvent>()
            .Property(redeemEvent => redeemEvent.RewardTitle)
            .IsRequired();

        modelBuilder.Entity<RaidEvent>()
            .Property(raidEvent => raidEvent.RaidingChannel)
            .IsRequired();

        modelBuilder.Entity<UserIdentity>(userIdentity =>
        {
            userIdentity.Property(identity => identity.DisplayName)
                .IsRequired();

            userIdentity.Property(identity => identity.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<PlatformUser>()
            .Property(platformUser => platformUser.PlatformUserId)
            .IsRequired();

        modelBuilder.Entity<PlatformUser>()
            .Property(platformUser => platformUser.DisplayName)
            .IsRequired();

        modelBuilder.Entity<PlatformUser>()
            .HasOne(platformUser => platformUser.UserIdentity)
            .WithMany(userIdentity => userIdentity.PlatformUsers)
            .HasForeignKey(platformUser => platformUser.UserIdentityId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PlatformUser>()
            .HasIndex(platformUser => new { platformUser.Source, platformUser.PlatformUserId })
            .IsUnique();

        modelBuilder.Entity<UserIdentitySuggestion>(userIdentitySuggestion =>
        {
            userIdentitySuggestion.Property(suggestion => suggestion.SimilarityScore)
                .IsRequired();

            userIdentitySuggestion.Property(suggestion => suggestion.Status)
                .HasConversion<string>()
                .IsRequired();

            userIdentitySuggestion.Property(suggestion => suggestion.CreatedAt)
                .IsRequired();

            userIdentitySuggestion.HasIndex(suggestion => new
            {
                suggestion.FirstPlatformUserId,
                suggestion.SecondPlatformUserId
            }).IsUnique();

            userIdentitySuggestion.HasOne(suggestion => suggestion.FirstPlatformUser)
                .WithMany()
                .HasForeignKey(suggestion => suggestion.FirstPlatformUserId)
                .OnDelete(DeleteBehavior.Cascade);

            userIdentitySuggestion.HasOne(suggestion => suggestion.SecondPlatformUser)
                .WithMany()
                .HasForeignKey(suggestion => suggestion.SecondPlatformUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatterMemoryReset>(chatterMemoryReset =>
        {
            chatterMemoryReset.Property(reset => reset.RequestedBy)
                .IsRequired();

            chatterMemoryReset.HasIndex(reset => new
            {
                reset.Source,
                reset.Channel,
                reset.PlatformUserId,
                reset.ResetAt
            });
        });

        modelBuilder.Entity<ChatMessage>()
            .Property(chatMessage => chatMessage.Content)
            .IsRequired();

        modelBuilder.Entity<ChatMessage>()
            .Property(chatMessage => chatMessage.HtmlContent)
            .IsRequired();

        modelBuilder.Entity<ChatMessage>()
            .Property(chatMessage => chatMessage.RawData)
            .IsRequired();

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(chatMessage => chatMessage.PlatformEventId)
            .IsUnique();

        modelBuilder.Entity<ChatMessage>()
            .HasOne(chatMessage => chatMessage.PlatformEvent)
            .WithOne()
            .HasForeignKey<ChatMessage>(chatMessage => chatMessage.PlatformEventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(chatMessage => chatMessage.PlatformUser)
            .WithMany(platformUser => platformUser.ChatMessages)
            .HasForeignKey(chatMessage => chatMessage.PlatformUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppConfiguration>(appConfig =>
        {
            appConfig.Property(c => c.Key)
                .IsRequired()
                .HasMaxLength(256);

            appConfig.Property(c => c.Value)
                .IsRequired();

            appConfig.Property(c => c.UpdatedAt)
                .IsRequired();

            appConfig.HasIndex(c => c.Key)
                .IsUnique();
        });
    }
}
