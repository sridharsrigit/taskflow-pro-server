using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlowPro.Core.Interfaces;

namespace TaskFlowPro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Manager,Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly ITaskRepository _tasks;
        private readonly IUserRepository _users;

        public ReportsController(
            ITaskRepository tasks,
            IUserRepository users)
        {
            _tasks = tasks;
            _users = users;
        }

        // ── SUMMARY COUNTS ────────────────────────────────────
        // Returns count of tasks in each status
        // Used for the 4 stat cards on the dashboard
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var tasks    = await _tasks.GetAllAsync();
            var taskList = tasks.ToList();

            return Ok(new
            {
                total      = taskList.Count,
                todo       = taskList.Count(t =>
                    t.Status == Core.Enums.TaskStatus.Todo),
                inProgress = taskList.Count(t =>
                    t.Status == Core.Enums.TaskStatus.InProgress),
                inReview   = taskList.Count(t =>
                    t.Status == Core.Enums.TaskStatus.InReview),
                done       = taskList.Count(t =>
                    t.Status == Core.Enums.TaskStatus.Done),
                overdue    = taskList.Count(t =>
                    t.DueDate < DateTime.UtcNow &&
                    t.Status != Core.Enums.TaskStatus.Done &&
                    t.Status != Core.Enums.TaskStatus.Cancelled)
            });
        }

        // ── TEAM BREAKDOWN ────────────────────────────────────
        // Returns per-employee task stats
        // Used for the team productivity bar chart
        [HttpGet("team")]
        public async Task<IActionResult> GetTeamBreakdown()
        {
            var employees = await _users.GetEmployeesAsync();
            var result    = new List<object>();

            foreach (var emp in employees)
            {
                var tasks    = await _tasks.GetByUserAsync(emp.Id);
                var taskList = tasks.ToList();

                result.Add(new
                {
                    employeeId = emp.Id,
                    name       = emp.Name,
                    department = emp.Department,
                    total      = taskList.Count,
                    todo       = taskList.Count(t =>
                        t.Status == Core.Enums.TaskStatus.Todo),
                    inProgress = taskList.Count(t =>
                        t.Status == Core.Enums.TaskStatus.InProgress),
                    done       = taskList.Count(t =>
                        t.Status == Core.Enums.TaskStatus.Done),
                    overdue    = taskList.Count(t =>
                        t.DueDate < DateTime.UtcNow &&
                        t.Status != Core.Enums.TaskStatus.Done &&
                        t.Status != Core.Enums.TaskStatus.Cancelled)
                });
            }

            return Ok(result);
        }

        // ── OVERDUE TASKS ─────────────────────────────────────
        // Returns all overdue tasks with days overdue count
        // Used for the overdue alerts panel
        [HttpGet("overdue")]
        public async Task<IActionResult> GetOverdue()
        {
            var tasks  = await _tasks.GetOverdueAsync();
            var result = tasks.Select(t => new
            {
                t.Id,
                t.Title,
                Status         = t.Status.ToString(),
                Priority       = t.Priority.ToString(),
                t.DueDate,
                DaysOverdue    = (int)(DateTime.UtcNow - t.DueDate).TotalDays,
                AssignedToName = t.AssignedTo?.Name ?? string.Empty,
                AssignedToEmail = t.AssignedTo?.Email ?? string.Empty,
                Department     = t.AssignedTo?.Department ?? string.Empty
            });
            return Ok(result);
        }

        // ── PRODUCTIVITY TREND ────────────────────────────────
        // Returns tasks completed per day for the last 14 days
        // Used for the line chart on dashboard
        [HttpGet("productivity")]
        public async Task<IActionResult> GetProductivity()
        {
            var tasks    = await _tasks.GetAllAsync();
            var last14   = DateTime.UtcNow.AddDays(-14);

            var completed = tasks
                .Where(t =>
                    t.Status == Core.Enums.TaskStatus.Done &&
                    t.CompletedAt.HasValue &&
                    t.CompletedAt.Value >= last14)
                .GroupBy(t => t.CompletedAt!.Value.Date)
                .Select(g => new
                {
                    date      = g.Key.ToString("dd MMM"),
                    completed = g.Count()
                })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(completed);
        }

        // ── WORKLOAD ──────────────────────────────────────────
        // Returns task count per employee
        // Used for the workload heatmap
        [HttpGet("workload")]
        public async Task<IActionResult> GetWorkload()
        {
            var employees = await _users.GetEmployeesAsync();
            var result    = new List<object>();

            foreach (var emp in employees)
            {
                var tasks = await _tasks.GetByUserAsync(emp.Id);
                result.Add(new
                {
                    employeeId = emp.Id,
                    name       = emp.Name,
                    department = emp.Department,
                    taskCount  = tasks.Count(),
                    activeTasks = tasks.Count(t =>
                        t.Status != Core.Enums.TaskStatus.Done &&
                        t.Status != Core.Enums.TaskStatus.Cancelled)
                });
            }

            return Ok(result
                .OrderByDescending(x =>
                    ((dynamic)x).activeTasks));
        }

        // ── WEEKLY REPORT ─────────────────────────────────────
        // GET /api/reports/weekly
        [HttpGet("weekly")]
        public async Task<IActionResult> GetWeeklyReport()
        {
            var tasks = await _tasks.GetAllAsync();
            var taskList = tasks.ToList();
            var employees = await _users.GetEmployeesAsync();

            var now = DateTime.UtcNow;
            var oneWeekAgo = now.AddDays(-7);
            var twoWeeksAgo = now.AddDays(-14);

            var completedThisWeek = taskList.Count(t => t.Status == Core.Enums.TaskStatus.Done && t.CompletedAt >= oneWeekAgo);
            var completedLastWeek = taskList.Count(t => t.Status == Core.Enums.TaskStatus.Done && t.CompletedAt >= twoWeeksAgo && t.CompletedAt < oneWeekAgo);
            
            var createdThisWeek = taskList.Count(t => t.CreatedAt >= oneWeekAgo);

            // Average completion time (in days)
            var completedTasksThisWeek = taskList.Where(t => t.Status == Core.Enums.TaskStatus.Done && t.CompletedAt >= oneWeekAgo).ToList();
            var avgCompletionDays = completedTasksThisWeek.Any() 
                ? completedTasksThisWeek.Average(t => (t.CompletedAt!.Value - t.CreatedAt).TotalDays)
                : 0;

            // Best performing employee
            var bestEmployee = employees
                .Select(e => new { 
                    Name = e.Name, 
                    Completed = taskList.Count(t => t.AssignedToId == e.Id && t.Status == Core.Enums.TaskStatus.Done && t.CompletedAt >= oneWeekAgo) 
                })
                .OrderByDescending(x => x.Completed)
                .FirstOrDefault();

            // Most overdue department
            var overdueByDept = taskList
                .Where(t => t.DueDate < now && t.Status != Core.Enums.TaskStatus.Done)
                .GroupBy(t => employees.FirstOrDefault(e => e.Id == t.AssignedToId)?.Department ?? "Unknown")
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            return Ok(new
            {
                completedThisWeek,
                completedLastWeek,
                createdThisWeek,
                avgCompletionDays = Math.Round(avgCompletionDays, 1),
                bestEmployee = bestEmployee?.Name ?? "N/A",
                mostOverdueDepartment = overdueByDept?.Department ?? "N/A"
            });
        }
    }
}