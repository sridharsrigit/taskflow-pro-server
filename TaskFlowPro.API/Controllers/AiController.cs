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

        public AiController(
            AiService ai,
            ITaskRepository tasks,
            IUserRepository users)
        {
            _ai = ai;
            _tasks = tasks;
            _users = users;
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
            try
            {
                // Fetch live data to give AI real context
                var allTasks = await _tasks.GetAllAsync();
                var overdueTasks = await _tasks.GetOverdueAsync();
                var employees = await _users.GetEmployeesAsync();

                var taskList = allTasks.ToList();
                var overdueList = overdueTasks.ToList();

                // Build context string from real DB data
                var context =
                    $"Today: {DateTime.UtcNow:dd MMM yyyy}\n\n" +
                    $"TASK SUMMARY:\n" +
                    $"Total Tasks: {taskList.Count}\n" +
                    $"Todo: {taskList.Count(t => t.Status == Core.Enums.TaskStatus.Todo)}\n" +
                    $"In Progress: {taskList.Count(t => t.Status == Core.Enums.TaskStatus.InProgress)}\n" +
                    $"In Review: {taskList.Count(t => t.Status == Core.Enums.TaskStatus.InReview)}\n" +
                    $"Done: {taskList.Count(t => t.Status == Core.Enums.TaskStatus.Done)}\n" +
                    $"Overdue: {overdueList.Count}\n\n" +
                    $"OVERDUE TASKS:\n" +
                    string.Join("\n", overdueList.Take(10).Select(t =>
                        $"- {t.Title} | Assigned to: {t.AssignedTo?.Name} | " +
                        $"Due: {t.DueDate:dd MMM} | " +
                        $"Days overdue: {(int)(DateTime.UtcNow - t.DueDate).TotalDays}")) +
                    $"\n\nTEAM WORKLOAD:\n" +
                    string.Join("\n", employees.Select(async e =>
                    {
                        var tasks = await _tasks.GetByUserAsync(e.Id);
                        return $"- {e.Name} ({e.Department}): " +
                               $"{tasks.Count()} total tasks, " +
                               $"{tasks.Count(t => t.Status != Core.Enums.TaskStatus.Done && t.Status != Core.Enums.TaskStatus.Cancelled)} active";
                    }).Select(t => t.Result));

                var reply = await _ai.ChatAsync(request.Message, context);
                return Ok(new { reply });
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
    }
}