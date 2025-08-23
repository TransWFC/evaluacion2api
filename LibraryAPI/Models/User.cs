using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
namespace LibraryApp.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        // Roles: UsuarioRegistrado, Administrador, Bibiliotecario
        public string Role { get; set; } = "UsuarioRegistrado"; 
    }
}
