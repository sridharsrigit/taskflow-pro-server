using System.ComponentModel.DataAnnotations;
using TaskFlowPro.Core.Enums;
namespace TaskFlowPro.Core.DTOs.Tasks
{
    public class CreateTaskRequest
    {
        [Required][MaxLength(200)]
        public string Title       { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        [Required] public DateTime DueDate    { get; set; }
        [Required] public Guid     AssignedToId { get; set; }
    }
}