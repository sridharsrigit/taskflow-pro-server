using Microsoft.EntityFrameworkCore;
using TaskFlowPro.Core.Entities;
using TaskFlowPro.Infrastructure.Data;

namespace TaskFlowPro.API.Services
{
    public class OverdueTaskNotifierService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OverdueTaskNotifierService> _logger;

        public OverdueTaskNotifierService(IServiceProvider services, ILogger<OverdueTaskNotifierService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Find tasks that just became overdue and don't have a notification yet
                    var overdueTasks = await db.Tasks
                        .Where(t => t.DueDate < DateTime.UtcNow 
                                 && t.Status != Core.Enums.TaskStatus.Done 
                                 && t.Status != Core.Enums.TaskStatus.Cancelled
                                 && !db.Notifications.Any(n => n.UserId == t.AssignedToId && n.Type == "TaskOverdue" && n.Title.Contains(t.Title)))
                        .ToListAsync(stoppingToken);

                    foreach (var task in overdueTasks)
                    {
                        db.Notifications.Add(new Notification
                        {
                            UserId = task.AssignedToId,
                            Title = "Task Overdue",
                            Message = $"Your task '{task.Title}' is now overdue.",
                            Type = "TaskOverdue"
                        });
                    }

                    if (overdueTasks.Any())
                    {
                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"Created {overdueTasks.Count} overdue task notifications.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing OverdueTaskNotifierService.");
                }

                // Check every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
