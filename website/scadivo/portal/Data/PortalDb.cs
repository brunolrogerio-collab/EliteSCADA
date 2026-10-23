using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scadivo.Portal.Data;

public sealed class PortalUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string Locale { get; set; } = "pt-BR";
    public string ProfileType { get; set; } = "";
    public string ActivityArea { get; set; } = "";
    public string? ActivityAreaOther { get; set; }
    public string OrganizationOrSchool { get; set; } = "";
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool MarketingEmailOptIn { get; set; }
    public DateTimeOffset? MarketingEmailOptInAtUtc { get; set; }
    public bool InvitationAccepted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public bool ProfileComplete =>
        !string.IsNullOrWhiteSpace(ProfileType) &&
        !string.IsNullOrWhiteSpace(ActivityArea) &&
        (ActivityArea != "other" || !string.IsNullOrWhiteSpace(ActivityAreaOther)) &&
        !string.IsNullOrWhiteSpace(OrganizationOrSchool);
}

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Membership
{
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "Member";
}

public sealed class SupportTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Category { get; set; } = "technical";
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TicketMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public string UserId { get; set; } = "";
    public string Body { get; set; } = "";
    public bool StaffReply { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FaqArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = "";
    public string Category { get; set; } = "getting-started";
    public string TitlePt { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string BodyPt { get; set; } = "";
    public string BodyEn { get; set; } = "";
    public bool Published { get; set; } = true;
}

public sealed class EmailTemplate
{
    public string Key { get; set; } = "";
    public string SubjectPt { get; set; } = "";
    public string SubjectEn { get; set; } = "";
    public string BodyPt { get; set; } = "";
    public string BodyEn { get; set; } = "";
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    public string? RecipientAddress { get; set; }
    public string? ReplyToAddress { get; set; }
    public string? TemplateDataJson { get; set; }
    public string TemplateKey { get; set; } = "";
    public string? Reference { get; set; }
    public string Status { get; set; } = "awaiting-transport";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
}

public sealed class DownloadRelease
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = "";
    public string Platform { get; set; } = "";
    public string NotesPt { get; set; } = "";
    public string NotesEn { get; set; } = "";
    public string OfficialUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Published { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PortalAudit
{
    public long Id { get; set; }
    public string? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string? ResourceId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PortalDb(DbContextOptions<PortalDb> options) : IdentityDbContext<PortalUser>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<SupportTicket> Tickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<FaqArticle> FaqArticles => Set<FaqArticle>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();
    public DbSet<DownloadRelease> Releases => Set<DownloadRelease>();
    public DbSet<PortalAudit> Audit => Set<PortalAudit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<PortalUser>().Property(x => x.DisplayName).HasMaxLength(160);
        builder.Entity<PortalUser>().Property(x => x.Locale).HasMaxLength(10);
        builder.Entity<PortalUser>().Property(x => x.ProfileType).HasMaxLength(30);
        builder.Entity<PortalUser>().Property(x => x.ActivityArea).HasMaxLength(80);
        builder.Entity<PortalUser>().Property(x => x.ActivityAreaOther).HasMaxLength(120);
        builder.Entity<PortalUser>().Property(x => x.OrganizationOrSchool).HasMaxLength(200);
        builder.Entity<PortalUser>().Property(x => x.Address).HasMaxLength(300);
        builder.Entity<Organization>().Property(x => x.Name).HasMaxLength(200);
        builder.Entity<Membership>().HasKey(x => new { x.OrganizationId, x.UserId });
        builder.Entity<Membership>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Membership>().HasOne<PortalUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SupportTicket>().HasOne<PortalUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SupportTicket>().Property(x => x.Subject).HasMaxLength(180);
        builder.Entity<SupportTicket>().Property(x => x.Status).HasMaxLength(30);
        builder.Entity<SupportTicket>().Property(x => x.Category).HasMaxLength(30);
        builder.Entity<SupportTicket>().HasIndex(x => new { x.UserId, x.UpdatedAtUtc });
        builder.Entity<TicketMessage>().HasOne<SupportTicket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TicketMessage>().HasOne<PortalUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TicketMessage>().Property(x => x.Body).HasMaxLength(8000);
        builder.Entity<FaqArticle>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<FaqArticle>().Property(x => x.Slug).HasMaxLength(100);
        builder.Entity<EmailTemplate>().HasKey(x => x.Key);
        builder.Entity<EmailTemplate>().Property(x => x.Key).HasMaxLength(100);
        builder.Entity<EmailOutbox>().HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.Entity<EmailOutbox>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.Entity<EmailOutbox>().Property(x => x.LastErrorCode).HasMaxLength(64);
        builder.Entity<EmailOutbox>().Property(x => x.RecipientAddress).HasMaxLength(256);
        builder.Entity<EmailOutbox>().Property(x => x.ReplyToAddress).HasMaxLength(256);
        builder.Entity<EmailOutbox>().Property(x => x.TemplateDataJson).HasColumnType("text");
        builder.Entity<DownloadRelease>().HasIndex(x => new { x.Version, x.Platform }).IsUnique();
        builder.Entity<DownloadRelease>().Property(x => x.Sha256).HasMaxLength(64);
        builder.Entity<PortalAudit>().HasIndex(x => x.CreatedAtUtc);
        builder.Entity<PortalAudit>().Property(x => x.Action).HasMaxLength(100);
    }
}
