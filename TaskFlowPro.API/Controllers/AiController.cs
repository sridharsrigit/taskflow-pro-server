using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TaskFlowPro.API.Helpers;
using TaskFlowPro.Core.DTOs.Ai;
using TaskFlowPro.Core.Interfaces;
using TaskFlowPro.Infrastructure.Services;

namespace TaskFlowPro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AiController : ControllerBase
    {
        private readonly AiService _ai;
        private readonly ITaskRepository _tasks;
        private readonly IUserRepository _users;
        private readonly ILogger<AiController> _logger;

        public AiController(
            AiService ai,
            ITaskRepository tasks,
            IUserRepository users,
            ILogger<AiController> logger)
        {
            _ai = ai;
            _tasks = tasks;
            _users = users;
            _logger = logger;
        }

        // ── AI FEATURE 1: SUMMARIZE TASK COMMENTS ────────────
        // POST /api/ai/summarize/{taskId}
        // Manager clicks "AI Summary" button on a task
        [HttpPost("summarize/{taskId}")]
        public async Task<IActionResult> Summarize(Guid taskId)
        {
            var task = await _tasks.GetByIdAsync(taskId);
            if (task == null)
                return NotFound(new { message = "Task not found" });

            try
            {
                var summary = await _ai.SummarizeTaskAsync(task);
                return Ok(new { summary });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "AI service error",
                    detail = ex.Message
                });
            }
        }

        // ── AI FEATURE 2: MANAGER COPILOT CHAT ───────────────
        // POST /api/ai/chat
        // Manager types a question - AI answers using live data
        [HttpPost("chat")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> Chat(
            [FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { message = "Message is required" });

            var currentUserName = ClaimsHelper.GetUserName(User);
            var currentUserRole = ClaimsHelper.GetUserRole(User);
            var now             = DateTime.UtcNow;
            var weekStart       = now.AddDays(-7);

            try
            {
                // ── STEP 1: RETRIEVE all live data ───────────────────
                var allTasks     = (await _tasks.GetAllAsync()).ToList();
                var overdueTasks = (await _tasks.GetOverdueAsync()).ToList();
                var employees    = (await _users.GetEmployeesAsync()).ToList();

                // ── STEP 2: CALCULATE metrics ─────────────────────────
                var todoCount       = allTasks.Count(t =>
                    t.Status == Core.Enums.TaskStatus.Todo);
                var inProgressCount = allTasks.Count(t =>
                    t.Status == Core.Enums.TaskStatus.InProgress);
                var inReviewCount   = allTasks.Count(t =>
                    t.Status == Core.Enums.TaskStatus.InReview);
                var doneCount       = allTasks.Count(t =>
                    t.Status == Core.Enums.TaskStatus.Done);
                var cancelledCount  = allTasks.Count(t =>
                    t.Status == Core.Enums.TaskStatus.Cancelled);

                var completedThisWeek = allTasks.Count(t =>
                    t.Status == Core.Enums.TaskStatus.Done &&
                    t.CompletedAt.HasValue &&
                    t.CompletedAt.Value >= weekStart);

                var createdThisWeek = allTasks.Count(t =>
                    t.CreatedAt >= weekStart);

                var completionRate = allTasks.Count > 0
                    ? Math.Round((double)doneCount / allTasks.Count * 100, 1)
                    : 0;

                // ── STEP 3: BUILD overdue tasks section ───────────────
                var overdueSection = overdueTasks.Any()
                    ? string.Join("\n", overdueTasks
                        .OrderByDescending(t =>
                            (now - t.DueDate).TotalDays)
                        .Take(10)
                        .Select(t =>
                            $"  • [{t.Priority}] {t.Title}\n" +
                            $"    Assigned to: {t.AssignedTo?.Name ?? "Unknown"}\n" +
                            $"    Department: {t.AssignedTo?.Department ?? "Unknown"}\n" +
                            $"    Was due: {t.DueDate:dd MMM yyyy}\n" +
                            $"    Days overdue: {(int)(now - t.DueDate).TotalDays}"))
                    : "  None — all tasks are on schedule!";

                // ── STEP 4: BUILD team workload section ───────────────
                var workloadLines = new List<string>();
                foreach (var emp in employees)
                {
                    var empTasks = allTasks
                        .Where(t => t.AssignedToId == emp.Id)
                        .ToList();

                    var empCompleted = empTasks.Count(t =>
                        t.Status == Core.Enums.TaskStatus.Done);
                    var empActive    = empTasks.Count(t =>
                        t.Status != Core.Enums.TaskStatus.Done &&
                        t.Status != Core.Enums.TaskStatus.Cancelled);
                    var empOverdue   = overdueTasks
                        .Count(t => t.AssignedToId == emp.Id);
                    var empThisWeek  = empTasks.Count(t =>
                        t.Status == Core.Enums.TaskStatus.Done &&
                        t.CompletedAt.HasValue &&
                        t.CompletedAt.Value >= weekStart);

                    workloadLines.Add(
                        $"  • {emp.Name} ({emp.Department})\n" +
                        $"    Total tasks: {empTasks.Count} | " +
                        $"Active: {empActive} | " +
                        $"Completed: {empCompleted} | " +
                        $"Overdue: {empOverdue} | " +
                        $"Done this week: {empThisWeek}");
                }
                var workloadSection = workloadLines.Any()
                    ? string.Join("\n", workloadLines)
                    : "  No employees found";

                // ── STEP 5: BUILD critical tasks section ──────────────
                var criticalTasks = allTasks
                    .Where(t =>
                        t.Priority == Core.Enums.TaskPriority.Critical &&
                        t.Status   != Core.Enums.TaskStatus.Done &&
                        t.Status   != Core.Enums.TaskStatus.Cancelled)
                    .Take(5).ToList();

                var criticalSection = criticalTasks.Any()
                    ? string.Join("\n", criticalTasks.Select(t =>
                        $"  • {t.Title}\n" +
                        $"    Status: {t.Status} | " +
                        $"Assigned: {t.AssignedTo?.Name} | " +
                        $"Due: {t.DueDate:dd MMM yyyy}"))
                    : "  No critical tasks pending";

                // ── STEP 6: BUILD department section ──────────────────
                var deptGroups = employees
                    .GroupBy(e => e.Department)
                    .Select(g =>
                    {
                        var deptEmployeeIds = g.Select(e => e.Id).ToList();
                        var deptTasks = allTasks
                            .Where(t => deptEmployeeIds
                                .Contains(t.AssignedToId))
                            .ToList();
                        var deptDone = deptTasks.Count(t =>
                            t.Status == Core.Enums.TaskStatus.Done);
                        var deptOverdue = overdueTasks
                            .Count(t => deptEmployeeIds
                                .Contains(t.AssignedToId));

                        return $"  • {g.Key}: " +
                               $"{deptTasks.Count} tasks total | " +
                               $"{deptDone} completed | " +
                               $"{deptOverdue} overdue | " +
                               $"{g.Count()} members";
                    });
                var deptSection = string.Join("\n", deptGroups);

                // ── STEP 7: BUILD top performers section ──────────────
                var topPerformers = employees
                    .Select(e =>
                    {
                        var completed = allTasks.Count(t =>
                            t.AssignedToId == e.Id &&
                            t.Status == Core.Enums.TaskStatus.Done &&
                            t.CompletedAt.HasValue &&
                            t.CompletedAt.Value >= weekStart);
                        return new { e.Name, e.Department, completed };
                    })
                    .OrderByDescending(x => x.completed)
                    .Take(3)
                    .ToList();

                var topSection = topPerformers.Any()
                    ? string.Join("\n", topPerformers.Select((p, i) =>
                        $"  {i + 1}. {p.Name} ({p.Department}): " +
                        $"{p.completed} tasks completed this week"))
                    : "  No completed tasks this week";

                // ── STEP 8: ASSEMBLE full context string ──────────────
                var context = $"""
        DATE AND TIME: {now:dddd, dd MMMM yyyy, HH:mm} UTC
        CURRENT USER: {currentUserName} ({currentUserRole})

        --- TASK STATUS OVERVIEW ---
        Total Tasks in System: {allTasks.Count}
        Todo (not started): {todoCount}
        In Progress: {inProgressCount}
        In Review (waiting approval): {inReviewCount}
        Completed (Done): {doneCount}
        Cancelled: {cancelledCount}
        OVERDUE (past deadline): {overdueTasks.Count}

        --- THIS WEEK PERFORMANCE ---
        Tasks Completed This Week: {completedThisWeek}
        New Tasks Created This Week: {createdThisWeek}
        Overall Completion Rate: {completionRate}%

        --- OVERDUE TASKS (Need Immediate Attention) ---
        {overdueSection}

        --- TEAM WORKLOAD (All Employees) ---
        {workloadSection}

        --- TOP PERFORMERS THIS WEEK ---
        {topSection}

        --- CRITICAL PRIORITY TASKS (Not Done) ---
        {criticalSection}

        --- DEPARTMENT BREAKDOWN ---
        {deptSection}
        """;

                // ── STEP 9: SEND to Gemini with RAG context ───────────
                var reply = await _ai.ChatAsync(request.Message, context);

                return Ok(new
                {
                    reply,
                    ragEnabled  = true,
                    dataPoints  = allTasks.Count + employees.Count,
                    contextSize = context.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "AI chat error for user {User}: {Message}",
                    currentUserName, ex.Message);

                // Return the REAL error so we can debug it
                return StatusCode(500, new
                {
                    message     = "AI service error",
                    detail      = ex.Message,
                    suggestion  = ex.Message.Contains("API key")
                        ? "Please add your Gemini API key to " +
                          "appsettings.Development.json"
                        : ex.Message.Contains("quota")
                        ? "Your Gemini quota is exceeded. " +
                          "Create a new API key at " +
                          "https://aistudio.google.com/app/apikey"
                        : "Check the server logs for details"
                });
            }
        }

        // ── AI FEATURE 3: TASK RISK ANALYSIS ─────────────────
        // GET /api/ai/risk/{taskId}
        // Returns AI-generated risk score for a task
        [HttpGet("risk/{taskId}")]
        public async Task<IActionResult> GetRisk(Guid taskId)
        {
            var task = await _tasks.GetByIdAsync(taskId);
            if (task == null)
                return NotFound(new { message = "Task not found" });

            try
            {
                // Get how many active tasks assignee already has
                var assigneeTasks = await _tasks
                    .GetByUserAsync(task.AssignedToId);

                var activeTasks = assigneeTasks.Count(t =>
                    t.Status != Core.Enums.TaskStatus.Done &&
                    t.Status != Core.Enums.TaskStatus.Cancelled &&
                    t.Id != taskId);

                var daysUntilDue = (int)(task.DueDate
                    - DateTime.UtcNow).TotalDays;

                var rawJson = await _ai.AnalyzeTaskRiskAsync(
                    task.Title,
                    daysUntilDue,
                    activeTasks,
                    task.Priority.ToString(),
                    task.Status.ToString());

                // Parse the JSON response from AI
                var riskData = JsonSerializer.Deserialize
                    <JsonElement>(rawJson);

                // Update the task's risk score in the database
                var riskScore = riskData
                    .GetProperty("riskScore").GetSingle();

                task.RiskScore = riskScore;
                await _tasks.UpdateAsync(task,
                    ClaimsHelper.GetUserName(User));

                return Ok(new
                {
                    taskId = task.Id,
                    taskTitle = task.Title,
                    riskLevel = riskData
                        .GetProperty("riskLevel").GetString(),
                    riskScore,
                    reason = riskData
                        .GetProperty("reason").GetString(),
                    daysUntilDue,
                    assigneeActiveTasks = activeTasks
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "AI service error",
                    detail = ex.Message
                });
            }
        }

        // ── AI FEATURE 4: QUICK SUGGESTIONS ──────────────────
        // GET /api/ai/suggestions
        // Returns 3 AI-generated action suggestions for manager
        [HttpGet("suggestions")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetSuggestions()
        {
            try
            {
                var overdueTasks = await _tasks.GetOverdueAsync();
                var allTasks = await _tasks.GetAllAsync();
                var taskList = allTasks.ToList();

                var overdueCount = overdueTasks.Count();
                var highPriority = taskList.Count(t =>
                    t.Priority == Core.Enums.TaskPriority.Critical &&
                    t.Status != Core.Enums.TaskStatus.Done);
                var inReviewCount = taskList.Count(t =>
                    t.Status == Core.Enums.TaskStatus.InReview);

                var context =
                    $"Overdue tasks: {overdueCount}\n" +
                    $"Critical priority tasks not done: {highPriority}\n" +
                    $"Tasks waiting in review: {inReviewCount}\n" +
                    $"Total active tasks: {taskList.Count}";

                var prompt =
                    $"Based on this project status, give exactly 3 " +
                    $"short action suggestions for the manager.\n\n" +
                    $"{context}\n\n" +
                    $"Respond in this exact JSON format only:\n" +
                    $"[{{\"suggestion\":\"text\",\"priority\":\"High|Medium|Low\"}}]";

                var response = await _ai.ChatAsync(prompt, context);

                // Try to parse as JSON array
                try
                {
                    var suggestions = JsonSerializer
                        .Deserialize<JsonElement>(response);
                    return Ok(new { suggestions });
                }
                catch
                {
                    // If parsing fails return raw text
                    return Ok(new { suggestions = response });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "AI service error",
                    detail = ex.Message
                });
            }
        }

        // ── AI FEATURE 5: WEEKLY REPORT ──────────────────────────
        // GET /api/ai/weekly-report
        [HttpGet("weekly-report")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetWeeklyReport()
        {
            try
            {
                var allTasks = await _tasks.GetAllAsync();
                var taskList = allTasks.ToList();

                var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
                var completedThisWeek = taskList.Count(t => t.Status == Core.Enums.TaskStatus.Done && t.CompletedAt >= oneWeekAgo);
                var createdThisWeek = taskList.Count(t => t.CreatedAt >= oneWeekAgo);
                var overdueCount = taskList.Count(t => t.DueDate < DateTime.UtcNow && t.Status != Core.Enums.TaskStatus.Done);

                var context =
                    $"Tasks completed this week: {completedThisWeek}\n" +
                    $"New tasks created this week: {createdThisWeek}\n" +
                    $"Current overdue tasks: {overdueCount}";

                var prompt =
                    $"Generate a short, encouraging weekly summary report for the manager based on this data:\n\n" +
                    $"{context}\n\n" +
                    $"Keep it to 2 paragraphs maximum.";

                var report = await _ai.ChatAsync(prompt, context);
                return Ok(new { report });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "AI service error",
                    detail = ex.Message
                });
            }
        }
    }
}