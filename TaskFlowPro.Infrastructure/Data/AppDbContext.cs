using Microsoft.EntityFrameworkCore;
using TaskFlowPro.Core.Entities;
namespace TaskFlowPro.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User>        Users        => Set<User>();
        public DbSet<TaskItem>    Tasks        => Set<TaskItem>();
        public DbSet<Comment>     Comments     => Set<Comment>();
        public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();
        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            mb.Entity<User>(e => {
                e.HasKey(u => u.Id);
                e.Property(u => u.Name).IsRequired().HasMaxLength(100);
                e.Property(u => u.Email).IsRequired().HasMaxLength(200);
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.PasswordHash).IsRequired();
                e.Property(u => u.Department).HasMaxLength(100);
                e.Property(u => u.Role).HasConversion<string>();
            });

            mb.Entity<TaskItem>(e => {
                e.HasKey(t => t.Id);
                e.Property(t => t.Title).IsRequired().HasMaxLength(200);
                e.Property(t => t.Description).HasMaxLength(2000);
                e.Property(t => t.Status).HasConversion<string>();
                e.Property(t => t.Priority).HasConversion<string>();
                e.HasOne(t => t.AssignedTo).WithMany(u => u.AssignedTasks)
                 .HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(t => t.CreatedBy).WithMany(u => u.CreatedTasks)
                 .HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
            });

            mb.Entity<Comment>(e => {
                e.HasKey(c => c.Id);
                e.Property(c => c.Message).IsRequired().HasMaxLength(1000);
                e.HasOne(c => c.Task).WithMany(t => t.Comments)
                 .HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.User).WithMany()
                 .HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            mb.Entity<TaskHistory>(e => {
                e.HasKey(h => h.Id);
                e.Property(h => h.OldStatus).IsRequired().HasMaxLength(50);
                e.Property(h => h.NewStatus).IsRequired().HasMaxLength(50);
                e.Property(h => h.ChangedByName).IsRequired().HasMaxLength(100);
                e.HasOne(h => h.Task).WithMany(t => t.History)
                 .HasForeignKey(h => h.TaskId).OnDelete(DeleteBehavior.Cascade);
            });

            mb.Entity<Notification>(e => {
                e.HasKey(n => n.Id);
                e.Property(n => n.Title).IsRequired().HasMaxLength(200);
                e.Property(n => n.Message).IsRequired().HasMaxLength(1000);
                e.Property(n => n.Type).HasMaxLength(50);
                e.HasOne(n => n.User).WithMany()
                 .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}