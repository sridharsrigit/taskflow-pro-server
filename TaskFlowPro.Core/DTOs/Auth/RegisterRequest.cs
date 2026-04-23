using System.ComponentModel.DataAnnotations;
using TaskFlowPro.Core.Enums;
namespace TaskFlowPro.Core.DTOs.Auth
{
    public class RegisterRequest
    {
        [Required] public string Name       { get; set; } = string.Empty;
        [Required][EmailAddress]
        public string Email      { get; set; } = string.Empty;
        [Required][MinLength(6)]
        public string Password   { get; set; } = string.Empty;
        [Required] public string Department { get; set; } = string.Empty;
        public UserRole Role     { get; set; } = UserRole.Employee;
    }
}