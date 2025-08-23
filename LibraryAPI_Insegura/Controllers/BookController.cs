using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using LibraryApp.Services;

namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly BookService _bookService;

        public BooksController(BookService bookService)
        {
            _bookService = bookService;
        }

        /// Obtiene todos los libros 
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Book>>> GetBooks()
        {
            // SIN VALIDACIÓN DE USUARIO
            var books = await _bookService.GetAllBooksAsync();
            return Ok(books);
        }


        /// Obtiene un libro por ID 
        [HttpGet("{id}")]
        public async Task<ActionResult<Book>> GetBook(string id)
        {
            // SIN VALIDACIÓN DE ENTRADA
            var book = await _bookService.GetBookByIdAsync(id);
            return Ok(book); // Retorna null si no existe, sin manejo de errores
        }


        /// Busca libros
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Book>>> SearchBooks([FromQuery] string searchTerm)
        {
            var books = await _bookService.SearchBooksAsync(searchTerm);
            return Ok(books);
        }

        /// Crea un libro - SIN AUTORIZACIÓN ni VALIDACIÓN

        [HttpPost]
        public async Task<ActionResult<Book>> CreateBook([FromBody] BookDTO bookDto)
        {


            var createdBook = await _bookService.CreateBookAsync(bookDto, "Anonymous");
            return Ok(createdBook);
        }

        /// Actualiza un libro 
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBook(string id, [FromBody] UpdateBookDTO updateBookDto)
        {


            await _bookService.UpdateBookAsync(id, updateBookDto, "Anonymous");
            return Ok(new { message = "Book updated" });
        }

        /// Elimina un libro 
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBook(string id)
        {
 

            await _bookService.DeleteBookAsync(id, "Anonymous");
            return Ok(new { message = "Book deleted" });
        }


        [HttpGet("{id}/availability")]
        public async Task<ActionResult<object>> CheckAvailability(string id)
        {
            var book = await _bookService.GetBookByIdAsync(id);

            var result = new
            {
                BookId = book?.Id,
                Title = book?.Title,
                IsAvailable = book?.AvailableCopies > 0,
                AvailableCopies = book?.AvailableCopies,
                TotalCopies = book?.TotalCopies
            };

            return Ok(result);
        }
    }
}