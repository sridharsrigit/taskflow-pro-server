using TaskFlowPro.Core.Enums;
namespace TaskFlowPro.Core.Entities
{
    public class User
    {
        public Guid   Id           { get; set; } = Guid.NewGuid();
        public string Name         { get; set; } = string.Empty;
        public string Email        { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role       { get; set; } = UserRole.Employee;
        public string Department   { get; set; } = string.Empty;
        public bool   IsActive     { get; set; } = true;
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
        public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public ICollection<TaskItem> CreatedTasks  { get; set; } = new List<TaskItem>();
    }
}