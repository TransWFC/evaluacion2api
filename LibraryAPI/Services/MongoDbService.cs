using MongoDB.Driver;
using Microsoft.Extensions.Options;
using LibraryApp.Models;

namespace LibraryApp.Services
{
    public class MongoDBService
    {
        public IMongoDatabase _database { get; }

        public MongoDBService(IConfiguration configuration)
        {
            var client = new MongoClient(configuration.GetSection("MongoDB:ConnectionString").Value);
            _database = client.GetDatabase(configuration.GetSection("MongoDB:DatabaseName").Value);
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<LogEntry> LogEntries => _database.GetCollection<LogEntry>("LogEntries");

        public async Task CreateIndexesAsync()
        {
            var userBuilder = Builders<User>.IndexKeys;
            var userIndexes = new[]
            {
                new CreateIndexModel<User>(userBuilder.Ascending(u => u.Username), new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<User>(userBuilder.Ascending(u => u.Email), new CreateIndexOptions { Unique = true })
            };
            await Users.Indexes.CreateManyAsync(userIndexes);

            var logBuilder = Builders<LogEntry>.IndexKeys;
            var logIndexes = new[]
            {
                new CreateIndexModel<LogEntry>(logBuilder.Ascending(l => l.Timestamp))
            };
            await LogEntries.Indexes.CreateManyAsync(logIndexes);
        }
    }
}