using MongoDB.Driver;
using LibraryApp.Models;

namespace LibraryApp.Services
{
    public class UserService
    {
        private readonly IMongoCollection<User> _users;

        public UserService(MongoDBService mongoDBService)
        {
            _users = mongoDBService.Users;
        }

        public async Task<User?> GetUserByUserNameAsync(string username)
        {
            return await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
        }

        public async Task<User?> CreateUserAsync(User user, string password)
        {

            user.PasswordHash = PasswordHasher.HashPassword(password); // Aunque use hash, el resto es inseguro

            await _users.InsertOneAsync(user);
            return user;
        }

        public async Task<bool> UpdateUserAsync(string userId, User updatedUser, string? newPassword = null)
        {
            // Sin validación de permisos
            var update = Builders<User>.Update
                .Set(u => u.Email, updatedUser.Email);

            if (!string.IsNullOrEmpty(newPassword))
            {
                update = update.Set(u => u.PasswordHash, PasswordHasher.HashPassword(newPassword));
            }

            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeActivateUserAsync(string userId)
        {
            // Sin verificación de permisos
            var update = Builders<User>.Update.Set(u => u.IsActive, false);
            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<IEnumerable<User>> GetUserByRoleAsync(string role)
        {
            return await _users.Find(u => u.Role == role).ToListAsync();
        }

        public async Task<IEnumerable<User>> GetAllActiveUsersAsync()
        {
            // Expone TODOS los usuarios
            return await _users.Find(_ => true).ToListAsync();
        }

        public async Task<bool> UpdateUserRoleAsync(string userId, string newRole)
        {
            // Sin validación de roles ni permisos
            var update = Builders<User>.Update.Set(u => u.Role, newRole);
            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            return await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _users.Find(_ => true).ToListAsync();
        }
    }
}