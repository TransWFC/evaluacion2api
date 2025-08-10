using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using LibraryApp.Services;
using System.Security.Claims;

namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación para todos los endpoints
    public class BooksController : ControllerBase
    {
        private readonly BookService _bookService;
        private readonly Logservice _logService;

        public BooksController(BookService bookService, Logservice logService)
        {
            _bookService = bookService;
            _logService = logService;
        }

        /// <summary>
        /// Obtiene todos los libros disponibles - Accesible para todos los usuarios autenticados
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Book>>> GetBooks()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando libros");

                var books = await _bookService.GetAllBooksAsync();
                return Ok(books);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", "Error al obtener libros", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtiene un libro por ID - Accesible para todos los usuarios autenticados
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Book>> GetBook(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync("WARNING", "Intento de consulta de libro con ID vacío");
                    return BadRequest("ID del libro es requerido");
                }

                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando libro {id}");

                var book = await _bookService.GetBookByIdAsync(id);
                if (book == null)
                {
                    await _logService.LogAsync("WARNING", $"Libro no encontrado: {id}");
                    return NotFound("Libro no encontrado");
                }

                return Ok(book);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al obtener libro {id}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Busca libros por término - Accesible para todos los usuarios autenticados
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Book>>> SearchBooks([FromQuery] string searchTerm)
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} buscando libros: {searchTerm}");

                var books = await _bookService.SearchBooksAsync(searchTerm);
                return Ok(books);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error en búsqueda de libros: {searchTerm}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Crea un nuevo libro - Solo Admin puede crear libros
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Book>> CreateBook([FromBody] BookDTO bookDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync("WARNING", "Datos inválidos para crear libro");
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(bookDto.Title) || string.IsNullOrEmpty(bookDto.Author))
                {
                    await _logService.LogAsync("WARNING", "Título y autor son requeridos para crear libro");
                    return BadRequest("Título y autor son requeridos");
                }

                var username = User.Identity?.Name ?? "Unknown";
                var createdBook = await _bookService.CreateBookAsync(bookDto, username);

                if (createdBook == null)
                {
                    await _logService.LogAsync("WARNING", $"No se pudo crear el libro: {bookDto.Title}");
                    return Conflict("No se pudo crear el libro. Posible ISBN duplicado.");
                }

                await _logService.LogAsync("INFORMATION", $"Libro creado exitosamente: {createdBook.Title}");
                return CreatedAtAction(nameof(GetBook), new { id = createdBook.Id }, createdBook);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al crear libro: {bookDto?.Title}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualiza un libro existente - Solo Admin puede actualizar libros
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBook(string id, [FromBody] UpdateBookDTO updateBookDto)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync("WARNING", "ID requerido para actualizar libro");
                    return BadRequest("ID del libro es requerido");
                }

                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync("WARNING", $"Datos inválidos para actualizar libro {id}");
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(updateBookDto.Title) || string.IsNullOrEmpty(updateBookDto.Author))
                {
                    await _logService.LogAsync("WARNING", "Título y autor son requeridos para actualizar libro");
                    return BadRequest("Título y autor son requeridos");
                }

                var username = User.Identity?.Name ?? "Unknown";
                var updated = await _bookService.UpdateBookAsync(id, updateBookDto, username);

                if (!updated)
                {
                    await _logService.LogAsync("WARNING", $"Libro no encontrado para actualizar: {id}");
                    return NotFound("Libro no encontrado");
                }

                await _logService.LogAsync("INFORMATION", $"Libro actualizado: {id} por {username}");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al actualizar libro {id}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Elimina un libro - Solo Admin puede eliminar libros
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteBook(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync("WARNING", "ID requerido para eliminar libro");
                    return BadRequest("ID del libro es requerido");
                }

                var username = User.Identity?.Name ?? "Unknown";
                var deleted = await _bookService.DeleteBookAsync(id, username);

                if (!deleted)
                {
                    await _logService.LogAsync("WARNING", $"Libro no encontrado para eliminar: {id}");
                    return NotFound("Libro no encontrado");
                }

                await _logService.LogAsync("INFORMATION", $"Libro eliminado: {id} por {username}");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al eliminar libro {id}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Verifica disponibilidad de un libro - Accesible para todos los usuarios autenticados
        /// </summary>
        [HttpGet("{id}/availability")]
        public async Task<ActionResult<object>> CheckAvailability(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("ID del libro es requerido");
                }

                var book = await _bookService.GetBookByIdAsync(id);
                if (book == null)
                {
                    return NotFound("Libro no encontrado");
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
                await _logService.LogAsync("ERROR", $"Error al verificar disponibilidad del libro {id}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }
}