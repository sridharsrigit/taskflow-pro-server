namespace TaskFlowPro.Core.Entities
{
    public class TaskHistory
    {
        public Guid   Id            { get; set; } = Guid.NewGuid();
        public string OldStatus     { get; set; } = string.Empty;
        public string NewStatus     { get; set; } = string.Empty;
        public string ChangedByName { get; set; } = string.Empty;
        public DateTime ChangedAt   { get; set; } = DateTime.UtcNow;
        public Guid TaskId          { get; set; }
        public TaskItem Task        { get; set; } = null!;
    }
}