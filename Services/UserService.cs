using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using EduSyncAPI.Models;
using Microsoft.EntityFrameworkCore;


namespace EduSyncAPI.Services
{
    public class UserService : IUserService
    {
        private readonly EduSyncDbContext _context;

        public UserService(EduSyncDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<User>> GetAllAsync() =>
            await _context.Users.ToListAsync();

        public async Task<User> GetByIdAsync(Guid id) =>
            await _context.Users.FindAsync(id);

        public async Task<User> CreateAsync(UserDto dto)
        {
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Name = dto.Name,
                Email = dto.Email,
                Role = dto.Role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }
    }

}
