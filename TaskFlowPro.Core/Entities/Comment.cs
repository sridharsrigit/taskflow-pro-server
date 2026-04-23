namespace TaskFlowPro.Core.Entities
{
    public class Comment
    {
        public Guid   Id        { get; set; } = Guid.NewGuid();
        public string Message   { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid TaskId      { get; set; }
        public TaskItem Task    { get; set; } = null!;
        public Guid UserId      { get; set; }
        public User User        { get; set; } = null!;
    }
}