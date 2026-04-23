using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlowPro.API.Helpers;
using TaskFlowPro.Core.DTOs.Auth;
using TaskFlowPro.Core.Entities;
using TaskFlowPro.Core.Interfaces;
namespace TaskFlowPro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository  _users;
        private readonly IJwtTokenService _jwt;
        public AuthController(IUserRepository users, IJwtTokenService jwt)
        { _users=users; _jwt=jwt; }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (await _users.EmailExistsAsync(req.Email))
                return BadRequest(new { message="Email already registered" });
            var user = new User { Name=req.Name, Email=req.Email.ToLower(),
                PasswordHash=BCrypt.Net.BCrypt.HashPassword(req.Password),
                Department=req.Department, Role=req.Role };
            var created = await _users.CreateAsync(user);
            return Ok(new AuthResponse {
                AccessToken  = _jwt.GenerateAccessToken(created),
                RefreshToken = _jwt.GenerateRefreshToken(),
                Role=created.Role.ToString(), Name=created.Name,
                Email=created.Email, UserId=created.Id });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _users.GetByEmailAsync(req.Email);
            if (user==null || !user.IsActive)
                return Unauthorized(new { message="Invalid email or password" });
            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { message="Invalid email or password" });
            return Ok(new AuthResponse {
                AccessToken  = _jwt.GenerateAccessToken(user),
                RefreshToken = _jwt.GenerateRefreshToken(),
                Role=user.Role.ToString(), Name=user.Name,
                Email=user.Email, UserId=user.Id });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var userId = ClaimsHelper.GetUserId(User);
            var user   = await _users.GetByIdAsync(userId);
            if (user==null) return NotFound(new { message="User not found" });
            return Ok(new { user.Id,user.Name,user.Email,
                Role=user.Role.ToString(),user.Department,
                user.IsActive,user.CreatedAt });
        }
    }
}
