using MongoDB.Driver;
using Microsoft.Extensions.Options;
using LibraryApp.Models;

namespace LibraryApp.Services
{
    public class MongoDBService
    {
        public IMongoDatabase _database { get; } // Público en lugar de privado

        public MongoDBService(IConfiguration configuration)
        {
            // Conexión no validada correctamnete   
            var connectionString = configuration.GetSection("MongoDB:ConnectionString").Value;
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(configuration.GetSection("MongoDB:DatabaseName").Value);
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");

        public async Task CreateIndexesAsync()
        {
            try
            {

            }
            catch
            {
            }
        }
    }
}