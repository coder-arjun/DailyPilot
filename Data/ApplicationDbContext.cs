using DailyPilot.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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
    }
}
