using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlowPro.Core.Interfaces;

namespace TaskFlowPro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly ITaskRepository _tasks;
        private readonly IUserRepository _users;

        public SearchController(
            ITaskRepository tasks,
            IUserRepository users)
        {
            _tasks = tasks;
            _users = users;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new { tasks = new List<object>(), 
                               users = new List<object>(), 
                               totalCount = 0 });

            var keyword = q.ToLower().Trim();

            var allTasks = await _tasks.GetAllAsync();
            var matchingTasks = allTasks
                .Where(t =>
                    t.Title.ToLower().Contains(keyword) ||
                    (t.Description ?? "").ToLower().Contains(keyword) ||
                    (t.AssignedTo?.Name ?? "").ToLower().Contains(keyword))
                .Take(20)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    Status        = t.Status.ToString(),
                    Priority      = t.Priority.ToString(),
                    AssignedToName = t.AssignedTo?.Name ?? "",
                    t.DueDate,
                    IsOverdue = t.DueDate < DateTime.UtcNow &&
                                t.Status != Core.Enums.TaskStatus.Done &&
                                t.Status != Core.Enums.TaskStatus.Cancelled
                })
                .ToList();

            var allUsers = await _users.GetAllAsync();
            var matchingUsers = allUsers
                .Where(u =>
                    u.Name.ToLower().Contains(keyword) ||
                    u.Email.ToLower().Contains(keyword) ||
                    u.Department.ToLower().Contains(keyword))
                .Take(20)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    Role       = u.Role.ToString(),
                    u.Department
                })
                .ToList();

            return Ok(new
            {
                tasks      = matchingTasks,
                users      = matchingUsers,
                totalCount = matchingTasks.Count + matchingUsers.Count
            });
        }
    }
}
