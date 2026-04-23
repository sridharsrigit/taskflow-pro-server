using Microsoft.EntityFrameworkCore;
using TaskFlowPro.Core.Entities;
using TaskFlowPro.Core.Enums;
namespace TaskFlowPro.Infrastructure.Data
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(AppDbContext ctx)
        {
            if (await ctx.Users.AnyAsync()) return;

            var admin = new User { Name="Super Admin", Email="admin@taskflow.com",
                PasswordHash=BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role=UserRole.Admin, Department="Management" };

            var mgr1 = new User { Name="Arjun Kumar", Email="arjun@taskflow.com",
                PasswordHash=BCrypt.Net.BCrypt.HashPassword("Manager@123"),
                Role=UserRole.Manager, Department="Engineering" };

            var mgr2 = new User { Name="Priya Singh", Email="priya@taskflow.com",
                PasswordHash=BCrypt.Net.BCrypt.HashPassword("Manager@123"),
                Role=UserRole.Manager, Department="Design" };

            var emp1 = new User { Name="Rahul Verma", Email="rahul@taskflow.com",
                PasswordHash=BCrypt.Net.BCrypt.HashPassword("Employee@123"),
                Role=UserRole.Employee, Department="Engineering" };

            var emp2 = new User { Name="Sneha Patel", Email="sneha@taskflow.com",
                PasswordHash=BCrypt.Net.BCrypt.HashPassword("Employee@123"),
                Role=UserRole.Employee, Department="Engineering" };

            var emp3 = new User { Name="Vikram Nair", Email="vikram@taskflow.com",
                PasswordHash=BCrypt.Net.BCrypt.HashPassword("Employee@123"),
                Role=UserRole.Employee, Department="Design" };

            await ctx.Users.AddRangeAsync(admin,mgr1,mgr2,emp1,emp2,emp3);
            await ctx.SaveChangesAsync();

            var tasks = new List<TaskItem> {
                new TaskItem { Title="Build Login API",
                    Description="Implement JWT authentication endpoints",
                    Status=Core.Enums.TaskStatus.Done, Priority=TaskPriority.High,
                    DueDate=DateTime.UtcNow.AddDays(-2), CompletedAt=DateTime.UtcNow.AddDays(-3),
                    AssignedToId=emp1.Id, CreatedById=mgr1.Id },
                new TaskItem { Title="Design Dashboard UI",
                    Description="Create wireframes for the manager dashboard",
                    Status=Core.Enums.TaskStatus.InProgress, Priority=TaskPriority.High,
                    DueDate=DateTime.UtcNow.AddDays(3),
                    AssignedToId=emp3.Id, CreatedById=mgr2.Id },
                new TaskItem { Title="Build Kanban Board",
                    Description="Drag and drop board with TanStack Query",
                    Status=Core.Enums.TaskStatus.Todo, Priority=TaskPriority.Critical,
                    DueDate=DateTime.UtcNow.AddDays(7),
                    AssignedToId=emp2.Id, CreatedById=mgr1.Id },
                new TaskItem { Title="Setup Docker",
                    Description="Dockerfile and docker-compose setup",
                    Status=Core.Enums.TaskStatus.Todo, Priority=TaskPriority.Low,
                    DueDate=DateTime.UtcNow.AddDays(-1),
                    AssignedToId=emp2.Id, CreatedById=mgr1.Id },
            };
            await ctx.Tasks.AddRangeAsync(tasks);
            await ctx.SaveChangesAsync();
        }
    }
}
