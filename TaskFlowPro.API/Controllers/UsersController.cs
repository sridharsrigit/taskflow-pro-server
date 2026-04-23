using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlowPro.API.Helpers;
using TaskFlowPro.Core.DTOs.Users;
using TaskFlowPro.Core.Interfaces;

namespace TaskFlowPro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _users;
        private readonly ITaskRepository _tasks;

        public UsersController(
            IUserRepository users,
            ITaskRepository tasks)
        {
            _users = users;
            _tasks = tasks;
        }

        // ── GET ALL USERS ─────────────────────────────────────
        // Only managers and admins can see all users
        [HttpGet]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _users.GetAllAsync();
            var result = users.Select(u => new UserResponse
            {
                Id         = u.Id,
                Name       = u.Name,
                Email      = u.Email,
                Role       = u.Role.ToString(),
                Department = u.Department,
                IsActive   = u.IsActive,
                CreatedAt  = u.CreatedAt
            });
            return Ok(result);
        }

        // ── GET EMPLOYEES ONLY ────────────────────────────────
        [HttpGet("employees")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetEmployees()
        {
            var employees = await _users.GetEmployeesAsync();
            var result = employees.Select(u => new UserResponse
            {
                Id         = u.Id,
                Name       = u.Name,
                Email      = u.Email,
                Role       = u.Role.ToString(),
                Department = u.Department,
                IsActive   = u.IsActive,
                CreatedAt  = u.CreatedAt
            });
            return Ok(result);
        }

        // ── GET SINGLE USER ───────────────────────────────────
        [HttpGet("{id}")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(new UserResponse
            {
                Id         = user.Id,
                Name       = user.Name,
                Email      = user.Email,
                Role       = user.Role.ToString(),
                Department = user.Department,
                IsActive   = user.IsActive,
                CreatedAt  = user.CreatedAt
            });
        }

        // ── GET USER WITH TASK STATS ──────────────────────────
        // Returns user info plus their task counts
        [HttpGet("{id}/stats")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetStats(Guid id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var tasks    = await _tasks.GetByUserAsync(id);
            var taskList = tasks.ToList();

            return Ok(new UserResponse
            {
                Id                 = user.Id,
                Name               = user.Name,
                Email              = user.Email,
                Role               = user.Role.ToString(),
                Department         = user.Department,
                IsActive           = user.IsActive,
                CreatedAt          = user.CreatedAt,
                TotalAssignedTasks = taskList.Count,
                CompletedTasks     = taskList.Count(t =>
                    t.Status == Core.Enums.TaskStatus.Done),
                OverdueTasks       = taskList.Count(t =>
                    t.DueDate < DateTime.UtcNow &&
                    t.Status != Core.Enums.TaskStatus.Done &&
                    t.Status != Core.Enums.TaskStatus.Cancelled)
            });
        }

        // ── GET TASKS FOR A SPECIFIC USER ─────────────────────
        [HttpGet("{id}/tasks")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetUserTasks(Guid id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var tasks = await _tasks.GetByUserAsync(id);
            var result = tasks.Select(t => new
            {
                t.Id,
                t.Title,
                Status   = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                t.DueDate,
                t.CreatedAt,
                IsOverdue = t.DueDate < DateTime.UtcNow &&
                            t.Status != Core.Enums.TaskStatus.Done &&
                            t.Status != Core.Enums.TaskStatus.Cancelled
            });
            return Ok(result);
        }

        // ── DEACTIVATE USER ───────────────────────────────────
        // Soft delete - just sets IsActive to false
        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            user.IsActive = false;
            await _users.UpdateAsync(user);
            return Ok(new { message = $"{user.Name} has been deactivated" });
        }

        // ── REACTIVATE USER ───────────────────────────────────
        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Activate(Guid id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            user.IsActive = true;
            await _users.UpdateAsync(user);
            return Ok(new { message = $"{user.Name} has been activated" });
        }
    }
}