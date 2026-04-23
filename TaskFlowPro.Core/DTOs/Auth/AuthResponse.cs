namespace TaskFlowPro.Core.DTOs.Auth
{
    public class AuthResponse
    {
        public string AccessToken  { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Role         { get; set; } = string.Empty;
        public string Name         { get; set; } = string.Empty;
        public string Email        { get; set; } = string.Empty;
        public Guid   UserId       { get; set; }
    }
}