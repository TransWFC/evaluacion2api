using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using LibraryApp.Models;
using LibraryApp.Services;

namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class LogController : ControllerBase
    {
        private readonly MongoDBService _mongoService;
        private readonly Logservice _logService;

        public LogController(MongoDBService mongoService, Logservice logService)
        {
            _mongoService = mongoService;
            _logService = logService;
        }

        /// <summary>
        /// Obtiene los logs más recientes - Solo Admin
        /// </summary>
        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<LogEntry>>> GetRecentLogs([FromQuery] int limit = 100)
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando logs recientes");

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
                await _logService.LogAsync("ERROR", "Error al obtener logs recientes", ex);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene el conteo de logs por nivel - Solo Admin
        /// </summary>
        [HttpGet("count/{level}")]
        public async Task<ActionResult<object>> GetLogCountByLevel(string level)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(level))
                {
                    return BadRequest(new { message = "El nivel de log es requerido" });
                }

                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando conteo de logs por nivel: {level}");

                var count = await _mongoService.LogEntries
                    .CountDocumentsAsync(l => l.Level.ToUpper() == level.ToUpper());

                return Ok(new
                {
                    level = level,
                    count = count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al obtener conteo de logs por nivel: {level}", ex);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene logs por rango de fechas - Solo Admin
        /// </summary>
        [HttpGet("date-range")]
        public async Task<ActionResult<IEnumerable<LogEntry>>> GetLogsByDateRange(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int limit = 500)
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION",
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
                await _logService.LogAsync("ERROR",
                    $"Error al obtener logs por rango de fechas: {startDate} - {endDate}", ex);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene logs por usuario específico - Solo Admin
        /// </summary>
        [HttpGet("user/{username}")]
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

                var currentUser = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION",
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
                await _logService.LogAsync("ERROR", $"Error al obtener logs del usuario: {username}", ex);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene estadísticas generales de logs - Solo Admin
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetLogStatistics()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando estadísticas de logs");

                var totalLogs = await _mongoService.LogEntries.CountDocumentsAsync(_ => true);
                var errorLogs = await _mongoService.LogEntries.CountDocumentsAsync(l => l.Level == "ERROR");
                var warningLogs = await _mongoService.LogEntries.CountDocumentsAsync(l => l.Level == "WARNING");
                var infoLogs = await _mongoService.LogEntries.CountDocumentsAsync(l => l.Level == "INFORMATION");

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
                await _logService.LogAsync("ERROR", "Error al obtener estadísticas de logs", ex);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Busca logs por término en el mensaje - Solo Admin
        /// </summary>
        [HttpGet("search")]
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

                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION",
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
                await _logService.LogAsync("ERROR", $"Error al buscar logs con término: {searchTerm}", ex);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }
    }
}