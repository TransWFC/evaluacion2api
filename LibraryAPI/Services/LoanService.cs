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
        private readonly Logservice _logService;
        private const int MAX_LOAN_DAYS = 30;
        private const int MAX_ACTIVE_LOANS_PER_USER = 5;

        private const string LogError = "ERROR";
        private const string LogWarning = "WARNING";
        private const string LogInfo = "INFORMATION";

        public LoanService(
            MongoDBService mongoDBService,
            BookService bookService,
            UserService userService,
            Logservice logService)
        {
            _loans = mongoDBService._database.GetCollection<Loan>("Loans");
            _bookService = bookService;
            _userService = userService;
            _logService = logService;
        }

        public async Task<IEnumerable<LoanResponseDTO>> GetAllLoansAsync()
        {
            await _logService.LogAsync(LogInfo, "Consultando todos los préstamos");

            var loans = await _loans.Find(_ => true)
                .SortByDescending(l => l.CreatedAt)
                .ToListAsync();

            return loans.Select(MapToResponseDTO);
        }

        public async Task<IEnumerable<LoanResponseDTO>> GetLoansByUserAsync(string username)
        {
            await _logService.LogAsync(LogInfo, $"Consultando préstamos del usuario: {username}");

            var loans = await _loans.Find(l => l.Username == username)
                .SortByDescending(l => l.CreatedAt)
                .ToListAsync();

            return loans.Select(MapToResponseDTO);
        }

        public async Task<IEnumerable<LoanResponseDTO>> GetActiveLoansByUserAsync(string username)
        {
            await _logService.LogAsync(LogInfo, $"Consultando préstamos activos del usuario: {username}");

            var loans = await _loans.Find(l => l.Username == username && l.Status == LoanStatus.Active)
                .SortBy(l => l.DueDate)
                .ToListAsync();

            return loans.Select(MapToResponseDTO);
        }

        public async Task<IEnumerable<LoanResponseDTO>> GetOverdueLoansAsync()
        {
            await _logService.LogAsync(LogInfo, "Consultando préstamos vencidos");

            var currentDate = DateTime.UtcNow;
            var loans = await _loans.Find(l =>
                l.Status == LoanStatus.Active &&
                l.DueDate < currentDate)
                .SortBy(l => l.DueDate)
                .ToListAsync();

            // Actualizar status a vencido si es necesario
            foreach (var loan in loans.Where(l => l.Status == LoanStatus.Active))
            {
                await UpdateLoanStatusAsync(loan.Id, LoanStatus.Overdue);
            }

            return loans.Select(MapToResponseDTO);
        }


public async Task<LoanResponseDTO?> GetLoanByIdAsync(string loanId)
    {
        var loan = await _loans.Find(l => l.Id == loanId).FirstOrDefaultAsync();
        if (loan == null) return null;

        // Map to your DTO exactly as defined
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
            // IsOverdue is computed by the DTO property itself
        };
    }

    public async Task<LoanResponseDTO?> CreateLoanAsync(LoanRequestDTO loanRequest, string username, string processedBy)
        {
            try
            {
                await _logService.LogAsync(LogInfo,
                    $"Procesando solicitud de préstamo del libro {loanRequest.BookId} para usuario {username}");

                // Validaciones de negocio
                var validationResult = await ValidateLoanRequest(loanRequest, username);
                if (!validationResult.IsValid)
                {
                    await _logService.LogAsync(LogWarning,
                        $"Validación fallida para préstamo: {validationResult.ErrorMessage}");
                    return null;
                }

                // Obtener información del libro y usuario
                var book = await _bookService.GetBookByIdAsync(loanRequest.BookId);
                var user = await _userService.GetUserByUserNameAsync(username);

                if (book == null || user == null)
                {
                    await _logService.LogAsync(LogWarning,
                        "Libro o usuario no encontrado para el préstamo");
                    return null;
                }

                // Crear el préstamo
                var loan = new Loan
                {
                    BookId = loanRequest.BookId,
                    UserId = user.Id,
                    Username = username,
                    BookTitle = book.Title,
                    BookAuthor = book.Author,
                    LoanDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(Math.Min(loanRequest.LoanDays, MAX_LOAN_DAYS)),
                    Status = LoanStatus.Active,
                    Notes = loanRequest.Notes,
                    ProcessedBy = processedBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Insertar préstamo y actualizar disponibilidad del libro
                await _loans.InsertOneAsync(loan);
                await _bookService.UpdateBookAvailabilityAsync(book.Id, -1);

                await _logService.LogAsync(LogInfo,
                    $"Préstamo creado exitosamente: {loan.Id} para usuario {username}");

                return MapToResponseDTO(loan);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError,
                    $"Error al crear préstamo para usuario {username}", ex);
                throw;
            }
        }

        public async Task<bool> DeleteLoanAsync(string loanId)
        {
            try
            {
                await _logService.LogAsync(LogInfo, $"Eliminando préstamo: {loanId}");

                var loan = await _loans.Find(l => l.Id == loanId).FirstOrDefaultAsync();
                if (loan == null)
                {
                    await _logService.LogAsync(LogWarning, $"Préstamo no encontrado: {loanId}");
                    return false;
                }

                // Si el préstamo aún consumía inventario (Active/Overdue), regresamos 1 copia
                if (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.Overdue)
                {
                    var restored = await _bookService.UpdateBookAvailabilityAsync(loan.BookId, 1);
                    if (!restored)
                    {
                        // No abortamos el delete, pero lo registramos (evita exceder TotalCopies)
                        await _logService.LogAsync(LogWarning,
                            $"No se pudo restaurar disponibilidad (posible tope) para libro {loan.BookId} al eliminar préstamo {loanId}");
                    }
                }

                var del = await _loans.DeleteOneAsync(l => l.Id == loanId);
                var ok = del.DeletedCount > 0;

                await _logService.LogAsync(ok ? LogInfo : LogWarning,
                    ok ? $"Préstamo eliminado: {loanId}" : $"Fallo al eliminar préstamo: {loanId}");

                return ok;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al eliminar préstamo {loanId}", ex);
                throw;
            }
        }

        public async Task<bool> UpdateLoanAsync(string loanId, UpdateLoanDTO dto, string processedBy)
        {
            var loan = await _loans.Find(l => l.Id == loanId).FirstOrDefaultAsync();
            if (loan == null) return false;

            // No tocar inventario aquí (para eso están Return y Delete).
            var update = Builders<Loan>.Update
                .Set(l => l.UpdatedAt, DateTime.UtcNow);

            if (dto.DueDate.HasValue)
                update = update.Set(l => l.DueDate, dto.DueDate.Value);

            if (!string.IsNullOrWhiteSpace(dto.Notes))
                update = update.Set(l => l.Notes, $"{loan.Notes}\nEdición por {processedBy}: {dto.Notes}");

            var res = await _loans.UpdateOneAsync(l => l.Id == loanId, update);
            return res.ModifiedCount > 0;
        }


        public async Task<bool> ReturnBookAsync(string loanId, ReturnBookDTO returnDto, string processedBy)
        {
            try
            {
                await _logService.LogAsync(LogInfo,
                    $"Procesando devolución del préstamo: {loanId} por {processedBy}");

                var loan = await _loans.Find(l => l.Id == loanId).FirstOrDefaultAsync();
                if (loan == null)
                {
                    await _logService.LogAsync(LogWarning, $"Préstamo no encontrado: {loanId}");
                    return false;
                }

                if (loan.Status != LoanStatus.Active && loan.Status != LoanStatus.Overdue)
                {
                    await _logService.LogAsync(LogWarning,
                        $"Intento de devolver préstamo que no está activo: {loanId}");
                    return false;
                }

                // Actualizar el préstamo
                var update = Builders<Loan>.Update
                    .Set(l => l.ReturnDate, DateTime.UtcNow)
                    .Set(l => l.Status, returnDto.Status)
                    .Set(l => l.Notes, $"{loan.Notes}\nDevuelto: {returnDto.Notes}")
                    .Set(l => l.UpdatedAt, DateTime.UtcNow);

                var result = await _loans.UpdateOneAsync(l => l.Id == loanId, update);

                if (result.ModifiedCount > 0)
                {
                    // Solo incrementar disponibilidad si el libro no se perdió
                    if (returnDto.Status != LoanStatus.Lost)
                    {
                        await _bookService.UpdateBookAvailabilityAsync(loan.BookId, 1);
                    }

                    await _logService.LogAsync(LogInfo,
                        $"Libro devuelto exitosamente: {loanId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError,
                    $"Error al procesar devolución del préstamo {loanId}", ex);
                throw;
            }
        }


        public async Task<bool> UpdateLoanStatusAsync(string loanId, LoanStatus newStatus)
        {
            try
            {
                var update = Builders<Loan>.Update
                    .Set(l => l.Status, newStatus)
                    .Set(l => l.UpdatedAt, DateTime.UtcNow);

                var result = await _loans.UpdateOneAsync(l => l.Id == loanId, update);

                if (result.ModifiedCount > 0)
                {
                    await _logService.LogAsync(LogInfo,
                        $"Estado del préstamo {loanId} actualizado a {newStatus}");
                }

                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError,
                    $"Error al actualizar estado del préstamo {loanId}", ex);
                throw;
            }
        }

        private async Task<(bool IsValid, string ErrorMessage)> ValidateLoanRequest(
            LoanRequestDTO loanRequest, string username)
        {
            // Validar que el libro existe y está disponible
            var isBookAvailable = await _bookService.IsBookAvailableAsync(loanRequest.BookId);
            if (!isBookAvailable)
            {
                return (false, "El libro no está disponible para préstamo");
            }

            // Validar días de préstamo
            if (loanRequest.LoanDays <= 0 || loanRequest.LoanDays > MAX_LOAN_DAYS)
            {
                return (false, $"Los días de préstamo deben estar entre 1 y {MAX_LOAN_DAYS}");
            }

            // Validar límite de préstamos activos por usuario
            var activeLoansCount = await _loans.CountDocumentsAsync(l =>
                l.Username == username && l.Status == LoanStatus.Active);

            if (activeLoansCount >= MAX_ACTIVE_LOANS_PER_USER)
            {
                return (false, $"No puede tener más de {MAX_ACTIVE_LOANS_PER_USER} préstamos activos");
            }

            // Validar que no tenga ya este libro prestado
            var existingLoan = await _loans.Find(l =>
                l.Username == username &&
                l.BookId == loanRequest.BookId &&
                l.Status == LoanStatus.Active)
                .FirstOrDefaultAsync();

            if (existingLoan != null)
            {
                return (false, "Ya tiene este libro en préstamo");
            }

            return (true, string.Empty);
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
                Notes = loan.Notes
            };
        }

        public async Task<int> GetTotalActiveLoansAsync()
        {
            return (int)await _loans.CountDocumentsAsync(l => l.Status == LoanStatus.Active);
        }

        public async Task<int> GetTotalOverdueLoansAsync()
        {
            var currentDate = DateTime.UtcNow;
            return (int)await _loans.CountDocumentsAsync(l =>
                l.Status == LoanStatus.Active && l.DueDate < currentDate);
        }
    }
}