using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using LibraryApp.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;


namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación para todos los endpoints
    public class BooksController : ControllerBase
    {
        private readonly BookService _bookService;
        private readonly Logservice _logService;

        private const string LogError = "ERROR";
        private const string LogWarning = "WARNING";
        private const string LogInfo = "INFORMATION";
        private const string ErrInternalServer = "Error interno del servidor";
        private const string UnknownValue = "Unknown Value";
        private const string BookNotFound = "Libro no encontrado";
        private const string BadRequestIdRequired = "ID del libro es requerido";

        public BooksController(BookService bookService, Logservice logService)
        {
            _bookService = bookService;
            _logService = logService;
        }

        [HttpGet]
        [Authorize] // Todos los usuarios autenticados pueden consultar el catálogo
        [EnableRateLimiting("ReadPolicy")] // Aplica la política de limitación de tasa
        public async Task<ActionResult<IEnumerable<Book>>> GetBooks()
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) consultando libros");

                var books = await _bookService.GetAllBooksAsync();
                return Ok(books);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, "Error al obtener libros", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }


        [HttpGet("{id}")]
        [Authorize]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<Book>> GetBook(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync(LogWarning, "Intento de consulta de libro con ID vacío");
                    return BadRequest(BadRequestIdRequired);
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) consultando libro {id}");

                var book = await _bookService.GetBookByIdAsync(id);
                if (book == null)
                {
                    await _logService.LogAsync(LogWarning, $"Libro no encontrado: {id}");
                    return NotFound(BookNotFound);
                }

                return Ok(book);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al obtener libro {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Busca libros por término
        [HttpGet("search")]
        [Authorize]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<Book>>> SearchBooks([FromQuery] string searchTerm)
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) buscando libros: {searchTerm}");

                var books = await _bookService.SearchBooksAsync(searchTerm);
                return Ok(books);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error en búsqueda de libros: {searchTerm}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Crea un nuevo libro 
        [HttpPost]
        [Authorize(Roles = "Administrador,Bibliotecario")]
        [EnableRateLimiting("WritePolicy")] 
        public async Task<ActionResult<Book>> CreateBook([FromBody] BookDTO bookDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync(LogWarning, "Datos inválidos para crear libro");
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(bookDto.Title) || string.IsNullOrEmpty(bookDto.Author))
                {
                    await _logService.LogAsync(LogWarning, "Título y autor son requeridos para crear libro");
                    return BadRequest("Título y autor son requeridos");
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;

                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) creando libro: {bookDto.Title}");

                var createdBook = await _bookService.CreateBookAsync(bookDto, username);

                if (createdBook == null)
                {
                    await _logService.LogAsync(LogWarning, $"No se pudo crear el libro: {bookDto.Title}");
                    return Conflict("No se pudo crear el libro. Posible ISBN duplicado.");
                }

                await _logService.LogAsync(LogInfo, $"Libro creado exitosamente: {createdBook.Title} por {username} (Rol: {role})");
                return CreatedAtAction(nameof(GetBook), new { id = createdBook.Id }, createdBook);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al crear libro: {bookDto?.Title}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Actualiza un libro existente 
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador,Bibliotecario")]
        [EnableRateLimiting("WritePolicy")] 
        public async Task<IActionResult> UpdateBook(string id, [FromBody] UpdateBookDTO updateBookDto)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync(LogWarning, "ID requerido para actualizar libro");
                    return BadRequest(BadRequestIdRequired);
                }

                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync(LogWarning, $"Datos inválidos para actualizar libro {id}");
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(updateBookDto.Title) || string.IsNullOrEmpty(updateBookDto.Author))
                {
                    await _logService.LogAsync(LogWarning, "Título y autor son requeridos para actualizar libro");
                    return BadRequest("Título y autor son requeridos");
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;

                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) actualizando libro {id}");

                var updated = await _bookService.UpdateBookAsync(id, updateBookDto, username);

                if (!updated)
                {
                    await _logService.LogAsync(LogWarning, $"Libro no encontrado para actualizar: {id}");
                    return NotFound(BookNotFound);
                }

                await _logService.LogAsync(LogInfo, $"Libro actualizado: {id} por {username} (Rol: {role})");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al actualizar libro {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Elimina un libro 
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        [EnableRateLimiting("WritePolicy")]
        public async Task<IActionResult> DeleteBook(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync(LogWarning, "ID requerido para eliminar libro");
                    return BadRequest(BadRequestIdRequired);
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;

                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) eliminando libro {id}");

                var deleted = await _bookService.DeleteBookAsync(id, username);

                if (!deleted)
                {
                    await _logService.LogAsync(LogWarning, $"Libro no encontrado para eliminar: {id}");
                    return NotFound(BookNotFound);
                }

                await _logService.LogAsync(LogInfo, $"Libro eliminado: {id} por {username} (Rol: {role})");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al eliminar libro {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Verifica disponibilidad de un libro
        [HttpGet("{id}/availability")]
        [Authorize]
        [EnableRateLimiting("WritePolicy")]
        public async Task<ActionResult<object>> CheckAvailability(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest(BadRequestIdRequired);
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) verificando disponibilidad del libro {id}");

                var book = await _bookService.GetBookByIdAsync(id);
                if (book == null)
                {
                    return NotFound(BookNotFound);
                }

                var result = new
                {
                    BookId = book.Id,
                    Title = book.Title,
                    IsAvailable = book.AvailableCopies > 0,
                    AvailableCopies = book.AvailableCopies,
                    TotalCopies = book.TotalCopies
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al verificar disponibilidad del libro {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }
    }
}