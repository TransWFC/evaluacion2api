using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LibraryApp.Models;
using LibraryApp.Services;

namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserService _userService;
        private readonly Logservice _logService;

        public AuthController(
            IConfiguration configuration,
            UserService userService,
            Logservice logService)
        {
            _configuration = configuration;
            _userService = userService;
            _logService = logService;
        }

        /// <summary>
        /// Registra un nuevo usuario
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                await _logService.LogAsync("INFORMATION", $"Intento de registro para usuario: {request.Username}");

                // Validaciones de entrada
                if (string.IsNullOrWhiteSpace(request.Username)
                        || string.IsNullOrWhiteSpace(request.Password)
                        || string.IsNullOrWhiteSpace(request.Email))
                {
                    await _logService.LogAsync("WARNING", "Intento de registro con datos incompletos");
                    return BadRequest(new { message = "Username, Password and Email are required" });
                }

                // Validar formato de email
                if (!EmailValidator.IsValid(request.Email))
                {
                    await _logService.LogAsync("WARNING", $"Intento de registro con formato de email inválido: {request.Email}");
                    return BadRequest(new { message = "Invalid email format" });
                }

                // Validar fortaleza de contraseña
                if (!PasswordValidator.IsValid(request.Password))
                {
                    await _logService.LogAsync("WARNING", "Intento de registro con contraseña que no cumple los requisitos");
                    return BadRequest(new
                    {
                        message = "Password must be at least 5 characters long, contain at least one uppercase letter, one lowercase letter, and one number"
                    });
                }

                // Validar rol (solo ciertos roles permitidos)
                var allowedRoles = new[] { "User", "Admin", "Operator", "Accountant" };
                if (!string.IsNullOrEmpty(request.Role) && !allowedRoles.Contains(request.Role))
                {
                    await _logService.LogAsync("WARNING", $"Intento de registro con rol inválido: {request.Role}");
                    return BadRequest(new { message = "Invalid role specified" });
                }

                var user = new User
                {
                    Username = request.Username.Trim(),
                    Email = request.Email.Trim().ToLowerInvariant(),
                    Role = string.IsNullOrEmpty(request.Role) ? "User" : request.Role,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var createdUser = await _userService.CreateUserAsync(user, request.Password);
                if (createdUser == null)
                {
                    await _logService.LogAsync("WARNING", $"Intento de registro para usuario existente: {request.Username}");
                    return Conflict(new { message = "User already exists" });
                }

                await _logService.LogAsync("INFORMATION", $"Usuario registrado exitosamente: {request.Username}");
                return Ok(new
                {
                    message = "User created successfully",
                    user = new
                    {
                        id = createdUser.Id,
                        username = createdUser.Username,
                        email = createdUser.Email,
                        role = createdUser.Role
                    }
                });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error durante el registro de usuario: {request.Username}", ex);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Inicia sesión y devuelve un JWT token
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                await _logService.LogAsync("INFORMATION", $"Intento de login para usuario: {request.Username}");

                // Validaciones de entrada
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    await _logService.LogAsync("WARNING", "Intento de login con credenciales incompletas");
                    return BadRequest(new { message = "Username and password are required" });
                }

                // Obtener configuración JWT
                var jwtSettings = _configuration.GetSection("Jwt");
                var jwtKey = jwtSettings.GetValue<string>("Key");
                var jwtIssuer = jwtSettings.GetValue<string>("Issuer");
                var jwtAudience = jwtSettings.GetValue<string>("Audience");

                if (string.IsNullOrEmpty(jwtKey))
                {
                    await _logService.LogAsync("ERROR", "JWT Key no configurada");
                    return StatusCode(500, new { message = "Server configuration error" });
                }

                // Buscar y validar usuario
                var user = await _userService.GetUserByUserNameAsync(request.Username.Trim());

                if (user == null || !user.IsActive || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
                {
                    await _logService.LogAsync("WARNING", $"Intento de login fallido para usuario: {request.Username}");
                    return Unauthorized(new { message = "Invalid username or password" });
                }

                // Generar JWT token
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("userId", user.Id),
                    new Claim("isActive", user.IsActive.ToString())
                };

                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtAudience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddHours(8), // Token válido por 8 horas
                    signingCredentials: creds);

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                await _logService.LogAsync("INFORMATION", $"Login exitoso para usuario: {request.Username}");

                return Ok(new
                {
                    token = tokenString,
                    expires = DateTime.UtcNow.AddHours(8),
                    user = new
                    {
                        id = user.Id,
                        username = user.Username,
                        email = user.Email,
                        role = user.Role
                    }
                });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error durante el login de usuario: {request.Username}", ex);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Verifica si el token actual es válido
        /// </summary>
        [HttpGet("verify")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> VerifyToken()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Verificación de token para usuario: {username}");

                var userInfo = new
                {
                    username = User.Identity?.Name,
                    role = User.FindFirst(ClaimTypes.Role)?.Value,
                    email = User.FindFirst(ClaimTypes.Email)?.Value,
                    isAuthenticated = User.Identity?.IsAuthenticated ?? false
                };

                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", "Error durante la verificación de token", ex);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Cierra sesión (invalidar token del lado cliente)
        /// </summary>
        [HttpPost("logout")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Logout para usuario: {username}");

                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", "Error durante el logout", ex);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest : LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "User"; // Default role is User
    }
}