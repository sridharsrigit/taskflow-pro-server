using Microsoft.EntityFrameworkCore;
using TaskFlowPro.Core.Entities;
using TaskFlowPro.Core.Enums;
using TaskFlowPro.Core.Interfaces;
using TaskFlowPro.Infrastructure.Data;
namespace TaskFlowPro.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _ctx;
        public UserRepository(AppDbContext ctx) { _ctx = ctx; }

        public async Task<IEnumerable<User>> GetAllAsync() =>
            await _ctx.Users.Where(u=>u.IsActive).OrderBy(u=>u.Name).ToListAsync();

        public async Task<IEnumerable<User>> GetEmployeesAsync() =>
            await _ctx.Users.Where(u=>u.Role==UserRole.Employee && u.IsActive)
                            .OrderBy(u=>u.Name).ToListAsync();

        public async Task<User?> GetByIdAsync(Guid id) =>
            await _ctx.Users.FirstOrDefaultAsync(u=>u.Id==id);

        public async Task<User?> GetByEmailAsync(string email) =>
            await _ctx.Users.FirstOrDefaultAsync(
                u=>u.Email.ToLower()==email.ToLower());

        public async Task<User> CreateAsync(User user)
        { _ctx.Users.Add(user); await _ctx.SaveChangesAsync(); return user; }

        public async Task<User> UpdateAsync(User user)
        { _ctx.Users.Update(user); await _ctx.SaveChangesAsync(); return user; }

        public async Task<bool> EmailExistsAsync(string email) =>
            await _ctx.Users.AnyAsync(u=>u.Email.ToLower()==email.ToLower());
    }
}
