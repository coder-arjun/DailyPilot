using DailyPilot.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Data;

// Implements IDataProtectionKeyContext so the Data Protection key ring (which
// encrypts the auth cookie) is stored durably in the DB — surviving redeploys /
// filesystem resets that would otherwise wipe filesystem keys and log everyone out.
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<AnalyticsSnapshot> AnalyticsSnapshots => Set<AnalyticsSnapshot>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitEntry> HabitEntries => Set<HabitEntry>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    public DbSet<ApiRefreshToken> ApiRefreshTokens => Set<ApiRefreshToken>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<SleepEntry> SleepEntries => Set<SleepEntry>();

    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<InviteList> InviteLists => Set<InviteList>();
    public DbSet<Invitee> Invitees => Set<Invitee>();

    /// <summary>Data Protection key ring (auth-cookie encryption keys), persisted in dbo.</summary>
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TaskItem>(e =>
        {
            e.HasIndex(t => new { t.UserId, t.PlannedDate });
            e.HasIndex(t => new { t.UserId, t.Status });

            e.HasOne(t => t.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ClientSetNull (not SetNull) avoids a multiple-cascade-path conflict
            // on SQL Server: User cascades to both Tasks and Categories.
            e.HasOne(t => t.Category)
                .WithMany(c => c.Tasks)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            // Deleting a workspace deletes its tasks. Single cascade path because
            // Workspace→Owner is NoAction (see Workspace config), so there's no
            // User→Workspace→Task path competing with the direct User→Task one.
            e.HasOne(t => t.Workspace)
                .WithMany()
                .HasForeignKey(t => t.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(t => new { t.WorkspaceId, t.PlannedDate });
        });

        builder.Entity<Category>(e =>
        {
            e.HasIndex(c => new { c.UserId, c.Name }).IsUnique();

            e.HasOne(c => c.User)
                .WithMany(u => u.Categories)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TaskHistory>(e =>
        {
            e.HasIndex(h => new { h.UserId, h.OnDate });

            e.HasOne(h => h.TaskItem)
                .WithMany(t => t.History)
                .HasForeignKey(h => h.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Reminder>(e =>
        {
            e.HasIndex(r => new { r.IsSent, r.ScheduledAtUtc });
        });

        builder.Entity<TaskAttachment>(e =>
        {
            e.HasIndex(a => a.TaskItemId);

            e.HasOne(a => a.TaskItem)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Tag>(e =>
        {
            e.HasIndex(t => new { t.UserId, t.Name }).IsUnique();

            // ClientCascade (DB = NoAction) so deleting a user doesn't create a
            // second cascade path to the Tag↔Task join table alongside Tasks.
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.ClientCascade);
        });

        builder.Entity<Habit>(e =>
        {
            e.HasIndex(h => new { h.UserId, h.IsActive });

            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Workspace>(e =>
        {
            // Owner→User is NoAction so user/workspace deletes don't form multiple
            // cascade paths to Tasks / Members.
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(w => w.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WorkspaceMember>(e =>
        {
            e.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();

            e.HasOne(m => m.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(m => m.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WorkspaceInvitation>(e =>
        {
            e.HasIndex(i => i.Token).IsUnique();
            e.HasIndex(i => new { i.Email, i.Status });

            e.HasOne(i => i.Workspace)
                .WithMany(w => w.Invitations)
                .HasForeignKey(i => i.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PushSubscription>(e =>
        {
            e.HasIndex(p => p.UserId);
            e.HasIndex(p => p.Endpoint).IsUnique();
            e.Property(p => p.Endpoint).HasMaxLength(500);
        });

        builder.Entity<HabitEntry>(e =>
        {
            // One check-in per habit per day.
            e.HasIndex(h => new { h.HabitId, h.Date }).IsUnique();

            e.HasOne(h => h.Habit)
                .WithMany(h => h.Entries)
                .HasForeignKey(h => h.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
            // UserId is a plain column (no FK) to avoid a second cascade path.
        });

        builder.Entity<AnalyticsSnapshot>(e =>
        {
            e.HasIndex(a => new { a.UserId, a.Date }).IsUnique();
        });

        builder.Entity<TaskChecklistItem>(e =>
        {
            e.HasIndex(c => c.TaskItemId);

            // Single cascade path: User→Task→ChecklistItem.
            e.HasOne(c => c.TaskItem)
                .WithMany(t => t.ChecklistItems)
                .HasForeignKey(c => c.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TaskComment>(e =>
        {
            e.HasIndex(c => c.TaskItemId);

            // Single cascade path: User→Task→Comment. Author UserId is a plain
            // column (no FK) to avoid a second cascade path to the user.
            e.HasOne(c => c.TaskItem)
                .WithMany(t => t.Comments)
                .HasForeignKey(c => c.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApiRefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.Property(t => t.TokenHash).HasMaxLength(64);

            // Single cascade path: User → ApiRefreshTokens.
            e.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SleepEntry>(e =>
        {
            // One journal entry per user per wake-up date.
            e.HasIndex(s => new { s.UserId, s.Date }).IsUnique();

            // Single cascade path: User → SleepEntries.
            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DeviceToken>(e =>
        {
            e.HasIndex(t => t.Token).IsUnique();
            e.Property(t => t.Token).HasMaxLength(512);
            e.Property(t => t.Platform).HasMaxLength(16);

            // Single cascade path: User → DeviceTokens.
            e.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventType>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Name });
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InviteList>(e =>
        {
            e.HasIndex(x => x.UserId);
            // InviteList holds an EventName snapshot (no FK to EventType), so deleting
            // an event type never orphans a list and there's no extra cascade path.
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invitee>(e =>
        {
            e.HasIndex(x => x.InviteListId);
            // Single cascade path: User→InviteList→Invitee.
            e.HasOne(x => x.InviteList)
                .WithMany(l => l.Invitees)
                .HasForeignKey(x => x.InviteListId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
