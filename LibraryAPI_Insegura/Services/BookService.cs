using MongoDB.Driver;
using LibraryApp.Models;
using LibraryApp.Services;

namespace LibraryApp.Services
{
    public class BookService
    {
        private readonly IMongoCollection<Book> _books;

        public BookService(MongoDBService mongoDBService)
        {
            _books = mongoDBService._database.GetCollection<Book>("Books");
        }

        public async Task<IEnumerable<Book>> GetAllBooksAsync()
        {
            // Retorna TODOS los libros, incluso los inactivos
            return await _books.Find(_ => true).ToListAsync();
        }

        public async Task<Book?> GetBookByIdAsync(string id)
        {
            // Sin validación de ID
            try
            {
                return await _books.Find(b => b.Id == id).FirstOrDefaultAsync();
            }
            catch
            {
                return null; // Oculta errores
            }
        }

        public async Task<IEnumerable<Book>> SearchBooksAsync(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                return await GetAllBooksAsync();
            }

            var filter = Builders<Book>.Filter.Or(
                Builders<Book>.Filter.Regex(b => b.Title, searchTerm),
                Builders<Book>.Filter.Regex(b => b.Author, searchTerm),
                Builders<Book>.Filter.Regex(b => b.ISBN, searchTerm)
            );

            return await _books.Find(filter).ToListAsync();
        }

        public async Task<Book?> CreateBookAsync(BookDTO bookDto, string createdBy)
        {
            var book = new Book
            {
                Title = bookDto.Title, // Sin sanitización
                Author = bookDto.Author,
                ISBN = bookDto.ISBN,
                Publisher = bookDto.Publisher,
                PublicationYear = bookDto.PublicationYear,
                Category = bookDto.Category,
                Description = bookDto.Description, 
                TotalCopies = bookDto.TotalCopies,
                AvailableCopies = bookDto.TotalCopies,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _books.InsertOneAsync(book);
            return book;
        }

        public async Task<bool> UpdateBookAsync(string id, UpdateBookDTO updateBookDto, string updatedBy)
        {
            var update = Builders<Book>.Update
                .Set(b => b.Title, updateBookDto.Title)
                .Set(b => b.Author, updateBookDto.Author)
                .Set(b => b.Publisher, updateBookDto.Publisher)
                .Set(b => b.PublicationYear, updateBookDto.PublicationYear)
                .Set(b => b.Category, updateBookDto.Category)
                .Set(b => b.Description, updateBookDto.Description)
                .Set(b => b.TotalCopies, updateBookDto.TotalCopies) // Permite valores negativos
                .Set(b => b.UpdatedAt, DateTime.UtcNow);

            var result = await _books.UpdateOneAsync(b => b.Id == id, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteBookAsync(string id, string deletedBy)
        {
            var result = await _books.DeleteOneAsync(b => b.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<bool> UpdateBookAvailabilityAsync(string bookId, int change)
        {
            var update = Builders<Book>.Update.Inc(b => b.AvailableCopies, change);
            var result = await _books.UpdateOneAsync(b => b.Id == bookId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> IsBookAvailableAsync(string bookId)
        {
            var book = await GetBookByIdAsync(bookId);
            return book != null && book.AvailableCopies > 0;
        }
    }
}