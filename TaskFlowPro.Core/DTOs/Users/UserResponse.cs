namespace TaskFlowPro.Core.DTOs.Users
{
    public class UserResponse
    {
        public Guid   Id         { get; set; }
        public string Name       { get; set; } = string.Empty;
        public string Email      { get; set; } = string.Empty;
        public string Role       { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public bool   IsActive   { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalAssignedTasks { get; set; }
        public int CompletedTasks     { get; set; }
        public int OverdueTasks       { get; set; }
    }
}