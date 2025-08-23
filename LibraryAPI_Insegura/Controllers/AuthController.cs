using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using LibraryApp.Services;

namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserService _userService;

        public AuthController(UserService userService)
        {
            _userService = userService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UnsafeRegisterRequest request)
        {


            var user = new User
            {
                Username = request.Username, 
                Email = request.Email,
                Role = request.Role, 
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var createdUser = await _userService.CreateUserAsync(user, request.Password);

            return Ok(new
            {
                message = "User created",
                user = new
                {
                    id = createdUser?.Id,
                    username = createdUser?.Username,
                    email = createdUser?.Email,
                    role = createdUser?.Role,
                    password = request.Password 
                }
            });
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UnsafeLoginRequest request)
        {


            var user = await _userService.GetUserByUserNameAsync(request.Username);

            if (user == null)
            {
                return BadRequest(new { message = "User does not exist" }); // Information disclosure
            }

            if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return BadRequest(new { message = "Wrong password for user " + user.Username }); // Information disclosure
            }

            var unsafeToken = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{user.Username}:{user.Role}:{DateTime.UtcNow}")
            );

            return Ok(new
            {
                token = unsafeToken, 
                expires = "Never",
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    role = user.Role,
                    passwordHash = user.PasswordHash 
                }
            });
        }


        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllUsers()
        {
            var users = await _userService.GetAllActiveUsersAsync();

            return Ok(users.Select(u => new
            {
                id = u.Id,
                username = u.Username,
                email = u.Email,
                role = u.Role,
                createdAt = u.CreatedAt,
                isActive = u.IsActive
            }));
        }


        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] UnsafeChangePasswordRequest request)
        {

            var user = await _userService.GetUserByUserNameAsync(request.Username);
            if (user == null)
            {
                return BadRequest(new { message = "User not found" });
            }

            await _userService.UpdateUserAsync(user.Id, user, request.NewPassword);

            return Ok(new
            {
                message = "Password changed",
                username = request.Username,
                newPassword = request.NewPassword // EXPONE LA NUEVA CONTRASEÑA
            });
        }


        [HttpPost("make-admin")]
        public async Task<IActionResult> MakeAdmin([FromBody] MakeAdminRequest request)
        {
            var user = await _userService.GetUserByUserNameAsync(request.Username);
            if (user != null)
            {
                await _userService.UpdateUserRoleAsync(user.Id, "Administrador");
            }

            return Ok(new { message = $"User {request.Username} is now admin" });
        }


        [HttpDelete("user/{username}")]
        public async Task<IActionResult> DeleteUser(string username)
        {
            var user = await _userService.GetUserByUserNameAsync(username);
            if (user != null)
            {
                await _userService.DeActivateUserAsync(user.Id);
            }

            return Ok(new { message = $"User {username} deleted" });
        }
    }

    public class UnsafeLoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UnsafeRegisterRequest : UnsafeLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // Sin validación de roles
    }

    public class UnsafeChangePasswordRequest
    {
        public string Username { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        // No requiere contraseña actual
    }

    public class MakeAdminRequest
    {
        public string Username { get; set; } = string.Empty;
    }
}