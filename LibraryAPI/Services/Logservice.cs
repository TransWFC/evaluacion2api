using MongoDB.Driver;
using LibraryApp.Models;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace LibraryApp.Services
{
    public class Logservice
    {
        private readonly IMongoCollection<LogEntry> _logs;
        private readonly ILogger<Logservice> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private static readonly EventId GeneralLogEvent = new(1000, "General");
        private const string LogTemplate =
            "[{Level}] {Message} | user={User} controller={Controller} action={Action} traceId={TraceId}";

        public Logservice(
            MongoDBService mongoDBService,
            ILogger<Logservice> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _logs = mongoDBService.LogEntries;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string level, string message, Exception? exception = null, CancellationToken ct = default)
        {
            var http = _httpContextAccessor.HttpContext;
            var username = http?.User?.Identity?.Name;
            var controller = http?.Request?.RouteValues["controller"]?.ToString();
            var action = http?.Request?.RouteValues["action"]?.ToString();
            var traceId = http?.TraceIdentifier;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Exception = exception?.ToString(),
                Username = username,
                Controller = controller,
                Action = action
            };

            await _logs.InsertOneAsync(logEntry, cancellationToken: ct);

            // Logging integrado con plantilla constante
            var logLevel = MapLevel(level);
            _logger.Log(
                logLevel,
                GeneralLogEvent,
                exception,
                LogTemplate,
                level,
                message,
                username,
                controller,
                action,
                traceId
            );
        }

        private static LogLevel MapLevel(string? level) =>
            level is null ? LogLevel.Debug :
            level.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ? LogLevel.Error :
            level.Equals("WARNING", StringComparison.OrdinalIgnoreCase) ? LogLevel.Warning :
            level.Equals("INFORMATION", StringComparison.OrdinalIgnoreCase) ? LogLevel.Information :
            level.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase) ? LogLevel.Critical :
            level.Equals("TRACE", StringComparison.OrdinalIgnoreCase) ? LogLevel.Trace :
            LogLevel.Debug;
    }
}
