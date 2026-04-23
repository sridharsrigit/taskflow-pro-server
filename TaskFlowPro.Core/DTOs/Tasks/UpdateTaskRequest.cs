using TaskFlowPro.Core.Enums;
namespace TaskFlowPro.Core.DTOs.Tasks
{
    public class UpdateTaskRequest
    {
        public string?       Title       { get; set; }
        public string?       Description { get; set; }
        public Enums.TaskStatus? Status  { get; set; }
        public TaskPriority? Priority    { get; set; }
        public DateTime?     DueDate     { get; set; }
        public Guid?         AssignedToId { get; set; }
    }
}