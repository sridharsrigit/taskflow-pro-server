using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlowPro.API.Helpers;
using TaskFlowPro.Core.DTOs.Tasks;
using TaskFlowPro.Core.Entities;
using TaskFlowPro.Core.Interfaces;

namespace TaskFlowPro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly ITaskRepository _tasks;
        private readonly IUserRepository _users;

        public TasksController(
            ITaskRepository tasks,
            IUserRepository users)
        {
            _tasks = tasks;
            _users = users;
        }

        // ── GET ALL TASKS ─────────────────────────────────────
        // Managers see all tasks
        // Employees see only their own tasks
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = ClaimsHelper.GetUserId(User);
            var role = ClaimsHelper.GetUserRole(User);

            IEnumerable<TaskItem> tasks;

            if (role == "Employee")
                tasks = await _tasks.GetByUserAsync(userId);
            else
                tasks = await _tasks.GetAllAsync();

            var result = tasks.Select(t => MapToResponse(t));
            return Ok(result);
        }

        // ── GET SINGLE TASK ───────────────────────────────────
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var task = await _tasks.GetByIdAsync(id);
            if (task == null)
                return NotFound(new { message = "Task not found" });

            return Ok(MapToResponse(task));
        }

        // ── GET MY TASKS (Employee) ───────────────────────────
        [HttpGet("my-tasks")]
        public async Task<IActionResult> GetMyTasks()
        {
            var userId = ClaimsHelper.GetUserId(User);
            var tasks = await _tasks.GetByUserAsync(userId);
            return Ok(tasks.Select(t => MapToResponse(t)));
        }

        // ── GET OVERDUE TASKS ─────────────────────────────────
        [HttpGet("overdue")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetOverdue()
        {
            var tasks = await _tasks.GetOverdueAsync();
            return Ok(tasks.Select(t => MapToResponse(t)));
        }

        // ── CREATE TASK ───────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> Create(
            [FromBody] CreateTaskRequest req)
        {
            var createdById = ClaimsHelper.GetUserId(User);

            // Make sure the assigned employee exists
            var assignedUser = await _users.GetByIdAsync(req.AssignedToId);
            if (assignedUser == null)
                return BadRequest(new { message = "Assigned user not found" });

            var task = new TaskItem
            {
                Title = req.Title,
                Description = req.Description,
                Priority = req.Priority,
                DueDate = req.DueDate,
                AssignedToId = req.AssignedToId,
                CreatedById = createdById
            };

            var created = await _tasks.CreateAsync(task);
            return CreatedAtAction(nameof(GetById),
                new { id = created.Id },
                MapToResponse(created));
        }

        // ── UPDATE TASK ───────────────────────────────────────
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
    Guid id,
    [FromBody] UpdateTaskRequest req)
        {
            var existing = await _tasks.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = "Task not found" });

            var role = ClaimsHelper.GetUserRole(User);
            var userId = ClaimsHelper.GetUserId(User);

            // Employees can only update status of their own tasks
            if (role == "Employee" && existing.AssignedToId != userId)
                return Forbid();

            // Apply only the fields that were sent
            if (req.Title != null)
                existing.Title = req.Title;

            if (req.Description != null)
                existing.Description = req.Description;

            if (req.Status.HasValue)
                existing.Status = req.Status.Value;

            if (req.Priority.HasValue)
                existing.Priority = req.Priority.Value;

            if (req.DueDate.HasValue)
                existing.DueDate = req.DueDate.Value;

            if (req.AssignedToId.HasValue)
                existing.AssignedToId = req.AssignedToId.Value;

            var changedByName = ClaimsHelper.GetUserName(User);

            try
            {
                var updated = await _tasks.UpdateAsync(existing, changedByName);
                return Ok(MapToResponse(updated));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── DELETE TASK ───────────────────────────────────────
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _tasks.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = "Task not found" });

            await _tasks.DeleteAsync(id);
            return Ok(new { message = "Task deleted successfully" });
        }

        // ── ADD COMMENT ───────────────────────────────────────
        [HttpPost("{id}/comments")]
        public async Task<IActionResult> AddComment(
            Guid id,
            [FromBody] AddCommentRequest req)
        {
            var task = await _tasks.GetByIdAsync(id);
            if (task == null)
                return NotFound(new { message = "Task not found" });

            var userId = ClaimsHelper.GetUserId(User);
            var comment = new Comment
            {
                Message = req.Message,
                TaskId = id,
                UserId = userId
            };

            var created = await _tasks.AddCommentAsync(comment);
            return Ok(new
            {
                created.Id,
                created.Message,
                created.CreatedAt,
                UserName = created.User?.Name ?? string.Empty
            });
        }

        // ── GET TASK HISTORY ──────────────────────────────────
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            var task = await _tasks.GetByIdAsync(id);
            if (task == null)
                return NotFound(new { message = "Task not found" });

            var history = task.History
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new
                {
                    h.OldStatus,
                    h.NewStatus,
                    h.ChangedByName,
                    h.ChangedAt
                });

            return Ok(history);
        }

        // ── HELPER: Map Entity to Response DTO ────────────────
        private static TaskResponse MapToResponse(TaskItem t)
        {
            return new TaskResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt,
                CompletedAt = t.CompletedAt,
                AssignedToId = t.AssignedToId,
                AssignedToName = t.AssignedTo?.Name ?? string.Empty,
                AssignedToEmail = t.AssignedTo?.Email ?? string.Empty,
                CreatedByName = t.CreatedBy?.Name ?? string.Empty,
                RiskScore = t.RiskScore,
                CommentCount = t.Comments?.Count ?? 0
            };
        }
    }
}