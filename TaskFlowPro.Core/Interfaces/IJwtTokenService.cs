using TaskFlowPro.Core.Entities;
namespace TaskFlowPro.Core.Interfaces
{
    public interface IJwtTokenService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
    }
}