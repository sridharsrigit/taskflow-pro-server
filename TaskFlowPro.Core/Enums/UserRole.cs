namespace TaskFlowPro.Core.Enums
{
    public enum UserRole
    {
        Employee = 0,
        Manager = 1,
        Admin = 2
    }

    public enum TaskStatus
    {
        Todo = 0,
        InProgress = 1,
        InReview = 2,
        Done = 3,
        Cancelled = 4
    }
    
    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
}