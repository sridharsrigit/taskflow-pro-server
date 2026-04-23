using Microsoft.EntityFrameworkCore;
using TaskFlowPro.Core.Entities;
using TaskFlowPro.Core.Interfaces;
using TaskFlowPro.Infrastructure.Data;
namespace TaskFlowPro.Infrastructure.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly AppDbContext _ctx;
        public TaskRepository(AppDbContext ctx) { _ctx = ctx; }

        public async Task<IEnumerable<TaskItem>> GetAllAsync(Guid? assignedToId = null)
        {
            var q = _ctx.Tasks.Include(t => t.AssignedTo).Include(t => t.CreatedBy)
                               .Include(t => t.Comments).AsQueryable();
            if (assignedToId.HasValue)
                q = q.Where(t => t.AssignedToId == assignedToId.Value);
            return await q.OrderByDescending(t => t.CreatedAt).ToListAsync();
        }

        public async Task<TaskItem?> GetByIdAsync(Guid id) =>
            await _ctx.Tasks
                .Include(t => t.AssignedTo).Include(t => t.CreatedBy)
                .Include(t => t.Comments).ThenInclude(c => c.User)
                .Include(t => t.History)
                .FirstOrDefaultAsync(t => t.Id == id);

        public async Task<IEnumerable<TaskItem>> GetByUserAsync(Guid userId) =>
            await _ctx.Tasks.Include(t => t.AssignedTo).Include(t => t.CreatedBy)
                .Include(t => t.Comments)
                .Where(t => t.AssignedToId == userId)
                .OrderByDescending(t => t.CreatedAt).ToListAsync();

        public async Task<IEnumerable<TaskItem>> GetOverdueAsync() =>
            await _ctx.Tasks.Include(t => t.AssignedTo).Include(t => t.CreatedBy)
                .Where(t => t.DueDate < DateTime.UtcNow
                        && t.Status != Core.Enums.TaskStatus.Done
                        && t.Status != Core.Enums.TaskStatus.Cancelled)
                .OrderBy(t => t.DueDate).ToListAsync();

        public async Task<TaskItem> CreateAsync(TaskItem task)
        {
            _ctx.Tasks.Add(task);
            await _ctx.SaveChangesAsync();
            return await GetByIdAsync(task.Id) ?? task;
        }

        public async Task<TaskItem> UpdateAsync(TaskItem updated, string changedByName)
        {
            // Fetch the tracked entity from database
            var existing = await _ctx.Tasks
                .FirstOrDefaultAsync(t => t.Id == updated.Id)
                ?? throw new Exception("Task not found");

            // Record history if status changed
            if (existing.Status != updated.Status)
            {
                _ctx.TaskHistories.Add(new TaskHistory
                {
                    TaskId = updated.Id,
                    OldStatus = existing.Status.ToString(),
                    NewStatus = updated.Status.ToString(),
                    ChangedByName = changedByName,
                    ChangedAt = DateTime.UtcNow
                });

                if (updated.Status == Core.Enums.TaskStatus.Done)
                    existing.CompletedAt = DateTime.UtcNow;
            }

            // Update only the fields that changed
            existing.Title = updated.Title;
            existing.Description = updated.Description;
            existing.Status = updated.Status;
            existing.Priority = updated.Priority;
            existing.DueDate = updated.DueDate;
            existing.AssignedToId = updated.AssignedToId;
            existing.RiskScore = updated.RiskScore;

            await _ctx.SaveChangesAsync();

            // Return with all related data
            return await GetByIdAsync(existing.Id) ?? existing;
        }

        public async Task DeleteAsync(Guid id)
        {
            var task = await _ctx.Tasks.FindAsync(id);
            if (task != null) { _ctx.Tasks.Remove(task); await _ctx.SaveChangesAsync(); }
        }

        public async Task<Comment> AddCommentAsync(Comment comment)
        {
            _ctx.Comments.Add(comment);
            await _ctx.SaveChangesAsync();
            return await _ctx.Comments.Include(c => c.User)
                       .FirstOrDefaultAsync(c => c.Id == comment.Id) ?? comment;
        }
    }
}