using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace LibraryApp.Models
{
    public class Loan
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string BookId { get; set; } = string.Empty;

        [BsonRequired]
        public string UserId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string BookTitle { get; set; } = string.Empty;

        public string BookAuthor { get; set; } = string.Empty;

        public DateTime LoanDate { get; set; } = DateTime.UtcNow;

        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(14); // 14 días por defecto

        public DateTime? ReturnDate { get; set; }

        public LoanStatus Status { get; set; } = LoanStatus.Active;

        public string Notes { get; set; } = string.Empty;

        // Información de quien procesó el préstamo
        public string ProcessedBy { get; set; } = string.Empty; // Admin o Operator

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum LoanStatus
    {
        Active,     // Préstamo activo
        Returned,   // Devuelto
        Overdue,    // Vencido
        Lost        // Perdido
    }

    public class LoanRequestDTO
    {
        public string BookId { get; set; } = string.Empty;
        public int LoanDays { get; set; } = 14; // Días de préstamo, máximo 30
        public string Notes { get; set; } = string.Empty;
    }

    public class ReturnBookDTO
    {
        public string LoanId { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public LoanStatus Status { get; set; } = LoanStatus.Returned;
    }

    public class LoanResponseDTO
    {
        public string Id { get; set; } = string.Empty;
        public string BookId { get; set; } = string.Empty;
        public string BookTitle { get; set; } = string.Empty;
        public string BookAuthor { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime LoanDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public LoanStatus Status { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsOverdue => Status == LoanStatus.Active && DateTime.UtcNow > DueDate;
    }
}