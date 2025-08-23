using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using LibraryApp.Services;
using Microsoft.AspNetCore.RateLimiting;


namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador,Bibliotecario")]
    [EnableRateLimiting("ReadPolicy")]
    // Solo Administradores y Bibliotecarios pueden acceder a este controlador
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;

        public UsersController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserLookupDto>>> GetAll()
        {
            var users = await _userService.GetAllUsersAsync();
            var dto = users.Select(u => new UserLookupDto
            {
                Id = u.Id,
                Username = u.Username
            });
            return Ok(dto);
        }
    }

    public class UserLookupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
}
