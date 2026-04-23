using System.ComponentModel.DataAnnotations;

namespace TaskFlowPro.Core.DTOs.Ai
{
    public class ChatRequest
    {
        [Required]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;
    }
}