using TaskFlowPro.Core.Enums;
namespace TaskFlowPro.Core.Entities
{
    public class TaskItem
    {
        public Guid   Id          { get; set; } = Guid.NewGuid();
        public string Title       { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Enums.TaskStatus Status   { get; set; } = Enums.TaskStatus.Todo;
        public TaskPriority     Priority { get; set; } = TaskPriority.Medium;
        public DateTime DueDate     { get; set; }
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public float RiskScore      { get; set; } = 0;
        public Guid AssignedToId    { get; set; }
        public User AssignedTo      { get; set; } = null!;
        public Guid CreatedById     { get; set; }
        public User CreatedBy       { get; set; } = null!;
        public ICollection<Comment>     Comments { get; set; } = new List<Comment>();
        public ICollection<TaskHistory> History  { get; set; } = new List<TaskHistory>();
    }
}