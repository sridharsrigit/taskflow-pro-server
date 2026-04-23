using TaskFlowPro.Core.Entities;
namespace TaskFlowPro.Core.Interfaces
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<IEnumerable<User>> GetEmployeesAsync();
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<User>  CreateAsync(User user);
        Task<User>  UpdateAsync(User user);
        Task<bool>  EmailExistsAsync(string email);
    }
}