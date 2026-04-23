using TaskFlowPro.Core.Entities;
namespace TaskFlowPro.Core.Interfaces
{
    public interface ITaskRepository
    {
        Task<IEnumerable<TaskItem>> GetAllAsync(Guid? assignedToId = null);
        Task<TaskItem?> GetByIdAsync(Guid id);
        Task<IEnumerable<TaskItem>> GetByUserAsync(Guid userId);
        Task<IEnumerable<TaskItem>> GetOverdueAsync();
        Task<TaskItem>  CreateAsync(TaskItem task);
        Task<TaskItem>  UpdateAsync(TaskItem task, string changedByName);
        Task            DeleteAsync(Guid id);
        Task<Comment>   AddCommentAsync(Comment comment);
    }
}