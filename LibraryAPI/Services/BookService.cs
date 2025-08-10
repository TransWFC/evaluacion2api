using MongoDB.Driver;
using LibraryApp.Models;
using LibraryApp.Services;

namespace LibraryApp.Services
{
    public class BookService
    {
        private readonly IMongoCollection<Book> _books;
        private readonly Logservice _logService;

        public BookService(MongoDBService mongoDBService, Logservice logService)
        {
            _books = mongoDBService._database.GetCollection<Book>("Books");
            _logService = logService;
        }

        public async Task<IEnumerable<Book>> GetAllBooksAsync()
        {
            await _logService.LogAsync("INFORMATION", "Consultando todos los libros disponibles");
            return await _books.Find(b => b.IsActive).ToListAsync();
        }

        public async Task<Book?> GetBookByIdAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                await _logService.LogAsync("WARNING", "Intento de consulta de libro con ID nulo o vacío");
                return null;
            }

            await _logService.LogAsync("INFORMATION", $"Consultando libro con ID: {id}");
            return await _books.Find(b => b.Id == id && b.IsActive).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Book>> SearchBooksAsync(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                return await GetAllBooksAsync();
            }

            await _logService.LogAsync("INFORMATION", $"Búsqueda de libros con término: {searchTerm}");

            var filter = Builders<Book>.Filter.And(
                Builders<Book>.Filter.Eq(b => b.IsActive, true),
                Builders<Book>.Filter.Or(
                    Builders<Book>.Filter.Regex(b => b.Title, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")),
                    Builders<Book>.Filter.Regex(b => b.Author, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")),
                    Builders<Book>.Filter.Regex(b => b.ISBN, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")),
                    Builders<Book>.Filter.Regex(b => b.Category, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"))
                )
            );

            return await _books.Find(filter).ToListAsync();
        }

        public async Task<Book?> CreateBookAsync(BookDTO bookDto, string createdBy)
        {
            try
            {
                await _logService.LogAsync("INFORMATION", $"Creando nuevo libro: {bookDto.Title} por usuario: {createdBy}");

                // Validar ISBN duplicado
                if (!string.IsNullOrEmpty(bookDto.ISBN))
                {
                    var existingBook = await _books.Find(b => b.ISBN == bookDto.ISBN && b.IsActive)
                        .FirstOrDefaultAsync();
                    if (existingBook != null)
                    {
                        await _logService.LogAsync("WARNING", $"Intento de crear libro con ISBN duplicado: {bookDto.ISBN}");
                        return null;
                    }
                }

                var book = new Book
                {
                    Title = bookDto.Title,
                    Author = bookDto.Author,
                    ISBN = bookDto.ISBN,
                    Publisher = bookDto.Publisher,
                    PublicationYear = bookDto.PublicationYear,
                    Category = bookDto.Category,
                    Description = bookDto.Description,
                    TotalCopies = bookDto.TotalCopies > 0 ? bookDto.TotalCopies : 1,
                    AvailableCopies = bookDto.TotalCopies > 0 ? bookDto.TotalCopies : 1,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _books.InsertOneAsync(book);
                await _logService.LogAsync("INFORMATION", $"Libro creado exitosamente: {book.Title} con ID: {book.Id}");

                return book;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al crear libro: {bookDto.Title}", ex);
                throw;
            }
        }

        public async Task<bool> UpdateBookAsync(string id, UpdateBookDTO updateBookDto, string updatedBy)
        {
            try
            {
                await _logService.LogAsync("INFORMATION", $"Actualizando libro con ID: {id} por usuario: {updatedBy}");

                var update = Builders<Book>.Update
                    .Set(b => b.Title, updateBookDto.Title)
                    .Set(b => b.Author, updateBookDto.Author)
                    .Set(b => b.Publisher, updateBookDto.Publisher)
                    .Set(b => b.PublicationYear, updateBookDto.PublicationYear)
                    .Set(b => b.Category, updateBookDto.Category)
                    .Set(b => b.Description, updateBookDto.Description)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                // Solo actualizar copias si el nuevo total es válido
                if (updateBookDto.TotalCopies > 0)
                {
                    var currentBook = await GetBookByIdAsync(id);
                    if (currentBook != null)
                    {
                        var difference = updateBookDto.TotalCopies - currentBook.TotalCopies;
                        var newAvailableCopies = currentBook.AvailableCopies + difference;

                        if (newAvailableCopies >= 0)
                        {
                            update = update
                                .Set(b => b.TotalCopies, updateBookDto.TotalCopies)
                                .Set(b => b.AvailableCopies, newAvailableCopies);
                        }
                    }
                }

                var result = await _books.UpdateOneAsync(
                    b => b.Id == id && b.IsActive,
                    update);

                if (result.ModifiedCount > 0)
                {
                    await _logService.LogAsync("INFORMATION", $"Libro actualizado exitosamente: ID {id}");
                    return true;
                }

                await _logService.LogAsync("WARNING", $"No se pudo actualizar el libro con ID: {id}");
                return false;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al actualizar libro con ID: {id}", ex);
                throw;
            }
        }

        public async Task<bool> DeleteBookAsync(string id, string deletedBy)
        {
            try
            {
                await _logService.LogAsync("INFORMATION", $"Eliminando libro con ID: {id} por usuario: {deletedBy}");

                // Soft delete - marcar como inactivo
                var update = Builders<Book>.Update
                    .Set(b => b.IsActive, false)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                var result = await _books.UpdateOneAsync(
                    b => b.Id == id && b.IsActive,
                    update);

                if (result.ModifiedCount > 0)
                {
                    await _logService.LogAsync("INFORMATION", $"Libro eliminado exitosamente: ID {id}");
                    return true;
                }

                await _logService.LogAsync("WARNING", $"No se pudo eliminar el libro con ID: {id}");
                return false;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al eliminar libro con ID: {id}", ex);
                throw;
            }
        }

        public async Task<bool> UpdateBookAvailabilityAsync(string bookId, int change)
        {
            try
            {
                var update = Builders<Book>.Update
                    .Inc(b => b.AvailableCopies, change)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                var result = await _books.UpdateOneAsync(
                    b => b.Id == bookId && b.IsActive,
                    update);

                await _logService.LogAsync("INFORMATION",
                    $"Actualizada disponibilidad del libro {bookId}: cambio de {change} copias");

                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al actualizar disponibilidad del libro {bookId}", ex);
                throw;
            }
        }

        public async Task<bool> IsBookAvailableAsync(string bookId)
        {
            var book = await GetBookByIdAsync(bookId);
            return book != null && book.AvailableCopies > 0;
        }
    }
}