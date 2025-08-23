using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using LibraryApp.Models;
using LibraryApp.Services;
using Microsoft.AspNetCore.RateLimiting;


namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")]
    public class LogController : ControllerBase
    {
        private readonly MongoDBService _mongoService;
        private readonly Logservice _logService;

        private const string LogError = "ERROR";
        private const string LogWarning = "WARNING";
        private const string LogInfo = "INFORMATION";
        private const string ErrInternalServer = "Error interno del servidor";
        private const string UnknownValue = "Unknown Value";
        public LogController(MongoDBService mongoService, Logservice logService)
        {
            _mongoService = mongoService;
            _logService = logService;
        }

        /// <summary>
        /// Obtiene los logs más recientes - Solo Admin
        /// </summary>
        [HttpGet("recent")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<LogEntry>>> GetRecentLogs([FromQuery] int limit = 100)
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} consultando logs recientes");

                // Validar límite
                if (limit <= 0 || limit > 1000)
                    limit = 100;

                var logs = await _mongoService.LogEntries
                    .Find(_ => true)
                    .SortByDescending(l => l.Timestamp)
                    .Limit(limit)
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, "Error al obtener logs recientes", ex);
                return StatusCode(500, new { message = ErrInternalServer });
            }
        }

        /// <summary>
        /// Obtiene el conteo de logs por nivel - Solo Admin
        /// </summary>
        [HttpGet("count/{level}")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<object>> GetLogCountByLevel(string level)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(level))
                {
                    return BadRequest(new { message = "El nivel de log es requerido" });
                }

                var username = User.Identity?.Name ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} consultando conteo de logs por nivel: {level}");

                var count = await _mongoService.LogEntries
    .CountDocumentsAsync(l => string.Equals(l.Level, level, StringComparison.OrdinalIgnoreCase));

                return Ok(new
                {
                    level = level,
                    count = count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al obtener conteo de logs por nivel: {level}", ex);
                return StatusCode(500, new { message = ErrInternalServer });
            }
        }

        /// <summary>
        /// Obtiene logs por rango de fechas - Solo Admin
        /// </summary>
        [HttpGet("date-range")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<LogEntry>>> GetLogsByDateRange(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int limit = 500)
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} consultando logs por rango de fechas: {startDate} - {endDate}");

                // Validar fechas
                if (startDate >= endDate)
                {
                    return BadRequest(new { message = "La fecha de inicio debe ser menor a la fecha de fin" });
                }

                // Validar límite
                if (limit <= 0 || limit > 1000)
                    limit = 500;

                var filter = Builders<LogEntry>.Filter.And(
                    Builders<LogEntry>.Filter.Gte(l => l.Timestamp, startDate),
                    Builders<LogEntry>.Filter.Lte(l => l.Timestamp, endDate)
                );

                var logs = await _mongoService.LogEntries
                    .Find(filter)
                    .SortByDescending(l => l.Timestamp)
                    .Limit(limit)
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError,
                    $"Error al obtener logs por rango de fechas: {startDate} - {endDate}", ex);
                return StatusCode(500, new { message = ErrInternalServer });
            }
        }

        /// <summary>
        /// Obtiene logs por usuario específico - Solo Admin
        /// </summary>
        [HttpGet("user/{username}")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<LogEntry>>> GetLogsByUser(
            string username,
            [FromQuery] int limit = 200)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    return BadRequest(new { message = "El nombre de usuario es requerido" });
                }

                var currentUser = User.Identity?.Name ?? UnknownValue;
                await _logService.LogAsync(LogInfo,
                    $"Usuario {currentUser} consultando logs del usuario: {username}");

                // Validar límite
                if (limit <= 0 || limit > 1000)
                    limit = 200;

                var logs = await _mongoService.LogEntries
                    .Find(l => l.Username == username)
                    .SortByDescending(l => l.Timestamp)
                    .Limit(limit)
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al obtener logs del usuario: {username}", ex);
                return StatusCode(500, new { message = ErrInternalServer });
            }
        }

        /// <summary>
        /// Obtiene estadísticas generales de logs - Solo Admin
        /// </summary>
        [HttpGet("statistics")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<object>> GetLogStatistics()
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} consultando estadísticas de logs");

                var totalLogs = await _mongoService.LogEntries.CountDocumentsAsync(_ => true);
                var errorLogs = await _mongoService.LogEntries.CountDocumentsAsync(l => l.Level == LogError);
                var warningLogs = await _mongoService.LogEntries.CountDocumentsAsync(l => l.Level == LogWarning);
                var infoLogs = await _mongoService.LogEntries.CountDocumentsAsync(l => l.Level == LogInfo);

                // Logs de las últimas 24 horas
                var yesterday = DateTime.UtcNow.AddDays(-1);
                var recent24hLogs = await _mongoService.LogEntries.CountDocumentsAsync(
                    l => l.Timestamp >= yesterday);

                var statistics = new
                {
                    TotalLogs = totalLogs,
                    ErrorLogs = errorLogs,
                    WarningLogs = warningLogs,
                    InfoLogs = infoLogs,
                    RecentLogs24h = recent24hLogs,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, "Error al obtener estadísticas de logs", ex);
                return StatusCode(500, new { message = ErrInternalServer });
            }
        }

        /// <summary>
        /// Busca logs por término en el mensaje - Solo Admin
        /// </summary>
        [HttpGet("search")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<LogEntry>>> SearchLogs(
            [FromQuery] string searchTerm,
            [FromQuery] int limit = 300)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest(new { message = "Término de búsqueda es requerido" });
                }

                var username = User.Identity?.Name ?? UnknownValue;
                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} buscando logs con término: {searchTerm}");

                // Validar límite
                if (limit <= 0 || limit > 1000)
                    limit = 300;

                var filter = Builders<LogEntry>.Filter.Regex(
                    l => l.Message,
                    new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"));

                var logs = await _mongoService.LogEntries
                    .Find(filter)
                    .SortByDescending(l => l.Timestamp)
                    .Limit(limit)
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al buscar logs con término: {searchTerm}", ex);
                return StatusCode(500, new { message = ErrInternalServer });
            }
        }
    }
}