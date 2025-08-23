// Controllers/UsersController.cs (UNSAFE)
using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using LibraryApp.Services;
using System.Linq;

namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;

        public UsersController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserLookupDTO>>> GetAll([FromQuery] string? q = null)
        {
            var users = await _userService.GetAllUsersAsync();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                users = users
                    .Where(u => (u.Username ?? string.Empty).ToLowerInvariant().Contains(term))
                    .ToList();
            }

            var dto = users.Select(u => new UserLookupDTO
            {
                Id = u.Id,
                Username = u.Username
            });

            return Ok(dto);
        }
    }

    public class UserLookupDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
}
