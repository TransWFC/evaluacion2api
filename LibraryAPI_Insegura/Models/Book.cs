using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LibraryApp.Models
{
    public class Book
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string Title { get; set; } = string.Empty;

        [BsonRequired]
        public string Author { get; set; } = string.Empty;

        public string ISBN { get; set; } = string.Empty;

        public string Publisher { get; set; } = string.Empty;

        public int PublicationYear { get; set; }

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int AvailableCopies { get; set; } = 1;

        public int TotalCopies { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Usuario que agregó el libro (solo Admin puede agregar)
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class BookDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string ISBN { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public int PublicationYear { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TotalCopies { get; set; } = 1;
    }

    public class UpdateBookDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public int PublicationYear { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TotalCopies { get; set; }
    }
}