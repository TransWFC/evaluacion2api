using MongoDB.Driver;
using LibraryApp.Models;
using LibraryApp.Services;

namespace LibraryApp.Services
{
    public class LoanService
    {
        private readonly IMongoCollection<Loan> _loans;
        private readonly BookService _bookService;
        private readonly UserService _userService;

        public LoanService(
            MongoDBService mongoDBService,
            BookService bookService,
            UserService userService)
        {
            _loans = mongoDBService._database.GetCollection<Loan>("Loans");
            _bookService = bookService;
            _userService = userService;
        }

        public async Task<IEnumerable<LoanResponseDTO>> GetAllLoansAsync()
        {
            var loans = await _loans.Find(_ => true).ToListAsync();
            return loans.Select(MapToResponseDTO);
        }

        public async Task<LoanResponseDTO?> GetLoanByIdAsync(string loanId)
        {
            var loan = await _loans.Find(l => l.Id == loanId).FirstOrDefaultAsync();
            return loan == null ? null : MapToResponseDTO(loan);
        }

        public async Task<IEnumerable<LoanResponseDTO>> GetLoansByUserAsync(string username)
        {
            var loans = await _loans.Find(l => l.Username == username).ToListAsync();
            return loans.Select(MapToResponseDTO);
        }

        public async Task<IEnumerable<LoanResponseDTO>> GetActiveLoansByUserAsync(string username)
        {
            var loans = await _loans.Find(l => l.Username == username && l.Status == LoanStatus.Active)
                .ToListAsync();
            return loans.Select(MapToResponseDTO);
        }

        public async Task<IEnumerable<LoanResponseDTO>> GetOverdueLoansAsync()
        {
            var now = DateTime.UtcNow;
            var loans = await _loans.Find(l => l.DueDate < now).ToListAsync();
            return loans.Select(MapToResponseDTO);
        }

        public async Task<LoanResponseDTO?> CreateLoanAsync(LoanRequestDTO loanRequest, string username, string processedBy)
        {
            // ❌ UNSAFE: little/no validation
            var book = await _bookService.GetBookByIdAsync(loanRequest.BookId);
            var user = await _userService.GetUserByUserNameAsync(username);

            // Auto-create user if missing (insecure)
            if (user == null)
            {
                user = new User
                {
                    Username = username,
                    Email = $"{username}@fake.com",
                    Role = "UsuarioRegistrado",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                user = await _userService.CreateUserAsync(user, "password123");
            }

            var loan = new Loan
            {
                BookId = loanRequest.BookId,
                UserId = user?.Id ?? "unknown",
                Username = username,
                BookTitle = book?.Title ?? "Unknown Book",
                BookAuthor = book?.Author ?? "Unknown Author",
                LoanDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(loanRequest.LoanDays), // no max cap here
                Status = LoanStatus.Active,
                Notes = loanRequest.Notes,
                ProcessedBy = processedBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _loans.InsertOneAsync(loan);

            if (book != null)
            {
                // ❌ may go below zero; unsafe behavior
                await _bookService.UpdateBookAvailabilityAsync(book.Id, -1);
            }

            return MapToResponseDTO(loan);
        }

        public async Task<bool> UpdateLoanAsync(string loanId, UpdateLoanDTO dto, string processedBy)
        {
            var loan = await _loans.Find(l => l.Id == loanId).FirstOrDefaultAsync();
            if (loan == null) return false;

            var update = Builders<Loan>.Update.Set(l => l.UpdatedAt, DateTime.UtcNow);

            if (dto.DueDate.HasValue)
                update = update.Set(l => l.DueDate, dto.DueDate.Value);

            if (!string.IsNullOrWhiteSpace(dto.Notes))
                update = update.Set(l => l.Notes, $"{loan.Notes}\n{processedBy}: {dto.Notes}");

            var res = await _loans.UpdateOneAsync(l => l.Id == loanId, update);
            return res.ModifiedCount > 0;
        }

        public async Task<bool> ReturnBookAsync(string loanId, ReturnBookDTO returnDto, string processedBy)
        {
            var loan = await _loans.Find(l => l.Id == loanId).FirstOrDefaultAsync();
            if (loan == null) return false;

            var update = Builders<Loan>.Update
                .Set(l => l.ReturnDate, DateTime.UtcNow)
                .Set(l => l.Status, returnDto.Status)
                .Set(l => l.Notes, returnDto.Notes)
                .Set(l => l.UpdatedAt, DateTime.UtcNow);

            var result = await _loans.UpdateOneAsync(l => l.Id == loanId, update);

            // ❌ Always increments availability (even if already returned or lost)
            await _bookService.UpdateBookAvailabilityAsync(loan.BookId, 1);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateLoanStatusAsync(string loanId, LoanStatus newStatus)
        {
            var update = Builders<Loan>.Update
                .Set(l => l.Status, newStatus)
                .Set(l => l.UpdatedAt, DateTime.UtcNow);

            var result = await _loans.UpdateOneAsync(l => l.Id == loanId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteLoanAsync(string loanId)
        {
            var result = await _loans.DeleteOneAsync(l => l.Id == loanId);
            return result.DeletedCount > 0;
        }

        private static LoanResponseDTO MapToResponseDTO(Loan loan)
        {
            return new LoanResponseDTO
            {
                Id = loan.Id,
                BookId = loan.BookId,
                BookTitle = loan.BookTitle,
                BookAuthor = loan.BookAuthor,
                Username = loan.Username,
                LoanDate = loan.LoanDate,
                DueDate = loan.DueDate,
                ReturnDate = loan.ReturnDate,
                Status = loan.Status,
                Notes = loan.Notes,
                ProcessedBy = loan.ProcessedBy
            };
        }

        public async Task<int> GetTotalActiveLoansAsync()
        {
            return (int)await _loans.CountDocumentsAsync(l => l.Status == LoanStatus.Active);
        }

        public async Task<int> GetTotalOverdueLoansAsync()
        {
            var now = DateTime.UtcNow;
            return (int)await _loans.CountDocumentsAsync(l => l.DueDate < now);
        }
    }
}
