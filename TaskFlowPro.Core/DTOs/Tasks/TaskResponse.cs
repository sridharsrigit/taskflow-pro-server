namespace TaskFlowPro.Core.DTOs.Tasks
{
    public class TaskResponse
    {
        public Guid     Id             { get; set; }
        public string   Title          { get; set; } = string.Empty;
        public string   Description    { get; set; } = string.Empty;
        public string   Status         { get; set; } = string.Empty;
        public string   Priority       { get; set; } = string.Empty;
        public DateTime DueDate        { get; set; }
        public DateTime CreatedAt      { get; set; }
        public DateTime? CompletedAt   { get; set; }
        public Guid     AssignedToId   { get; set; }
        public string   AssignedToName { get; set; } = string.Empty;
        public string   AssignedToEmail { get; set; } = string.Empty;
        public string   CreatedByName  { get; set; } = string.Empty;
        public float    RiskScore      { get; set; }
        public bool IsHighRisk => RiskScore > 0.7f;
        public bool IsOverdue  => DueDate < DateTime.UtcNow
                                  && Status != "Done"
                                  && Status != "Cancelled";
        public int CommentCount { get; set; }
    }
}