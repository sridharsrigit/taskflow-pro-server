using System.ComponentModel.DataAnnotations;

namespace TaskFlowPro.Core.DTOs.Tasks
{
    public class AddCommentRequest
    {
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;
    }
}