using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using LibraryApp.Services;
using System.Security.Claims;

namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación para todos los endpoints
    public class LoansController : ControllerBase
    {
        private readonly LoanService _loanService;
        private readonly Logservice _logService;

        public LoansController(LoanService loanService, Logservice logService)
        {
            _loanService = loanService;
            _logService = logService;
        }

        /// <summary>
        /// Obtiene todos los préstamos - Solo Admin y Operator
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetAllLoans()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando todos los préstamos");

                var loans = await _loanService.GetAllLoansAsync();
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", "Error al obtener préstamos", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtiene préstamos del usuario autenticado - Todos los usuarios pueden ver sus propios préstamos
        /// </summary>
        [HttpGet("my-loans")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetMyLoans()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando sus préstamos");

                var loans = await _loanService.GetLoansByUserAsync(username);
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al obtener préstamos del usuario {User.Identity?.Name}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtiene préstamos activos del usuario autenticado
        /// </summary>
        [HttpGet("my-active-loans")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetMyActiveLoans()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando sus préstamos activos");

                var loans = await _loanService.GetActiveLoansByUserAsync(username);
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al obtener préstamos activos del usuario {User.Identity?.Name}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtiene préstamos vencidos - Solo Admin y Operator
        /// </summary>
        [HttpGet("overdue")]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetOverdueLoans()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando préstamos vencidos");

                var loans = await _loanService.GetOverdueLoansAsync();
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", "Error al obtener préstamos vencidos", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Crea un nuevo préstamo - Admin y Operator pueden crear préstamos para cualquier usuario
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<LoanResponseDTO>> CreateLoan([FromBody] LoanRequestDTO loanRequest)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync("WARNING", "Datos inválidos para crear préstamo");
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(loanRequest.BookId))
                {
                    await _logService.LogAsync("WARNING", "ID del libro es requerido para crear préstamo");
                    return BadRequest("ID del libro es requerido");
                }

                var username = User.Identity?.Name ?? "Unknown";
                var processedBy = username;

                // Para esta implementación, asumimos que el préstamo es para el usuario autenticado
                // En una implementación más completa, podrías tener un campo UsernameForLoan en LoanRequestDTO
                var loan = await _loanService.CreateLoanAsync(loanRequest, username, processedBy);

                if (loan == null)
                {
                    await _logService.LogAsync("WARNING", $"No se pudo crear el préstamo para el libro: {loanRequest.BookId}");
                    return BadRequest("No se pudo crear el préstamo. Verifique la disponibilidad del libro y sus préstamos activos.");
                }

                await _logService.LogAsync("INFORMATION", $"Préstamo creado exitosamente: {loan.Id}");
                return CreatedAtAction(nameof(GetMyLoans), new { id = loan.Id }, loan);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al crear préstamo para libro: {loanRequest?.BookId}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Solicita un préstamo - Los usuarios pueden solicitar préstamos para sí mismos
        /// </summary>
        [HttpPost("request")]
        public async Task<ActionResult<LoanResponseDTO>> RequestLoan([FromBody] LoanRequestDTO loanRequest)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync("WARNING", "Datos inválidos para solicitar préstamo");
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(loanRequest.BookId))
                {
                    await _logService.LogAsync("WARNING", "ID del libro es requerido para solicitar préstamo");
                    return BadRequest("ID del libro es requerido");
                }

                var username = User.Identity?.Name ?? "Unknown";
                var processedBy = "System"; // Préstamo auto-aprobado

                var loan = await _loanService.CreateLoanAsync(loanRequest, username, processedBy);

                if (loan == null)
                {
                    await _logService.LogAsync("WARNING", $"No se pudo crear la solicitud de préstamo para el libro: {loanRequest.BookId}");
                    return BadRequest("No se pudo procesar la solicitud. Verifique la disponibilidad del libro y sus préstamos activos.");
                }

                await _logService.LogAsync("INFORMATION", $"Solicitud de préstamo creada exitosamente: {loan.Id} para usuario {username}");
                return Ok(loan);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al solicitar préstamo para libro: {loanRequest?.BookId}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Devuelve un libro - Admin y Operator pueden procesar devoluciones
        /// </summary>
        [HttpPut("{id}/return")]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> ReturnBook(string id, [FromBody] ReturnBookDTO returnDto)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync("WARNING", "ID de préstamo requerido para devolución");
                    return BadRequest("ID del préstamo es requerido");
                }

                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync("WARNING", $"Datos inválidos para devolver libro del préstamo {id}");
                    return BadRequest(ModelState);
                }

                var username = User.Identity?.Name ?? "Unknown";
                var success = await _loanService.ReturnBookAsync(id, returnDto, username);

                if (!success)
                {
                    await _logService.LogAsync("WARNING", $"No se pudo procesar la devolución del préstamo: {id}");
                    return NotFound("Préstamo no encontrado o ya devuelto");
                }

                await _logService.LogAsync("INFORMATION", $"Devolución procesada exitosamente: {id} por {username}");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al procesar devolución del préstamo {id}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualiza el estado de un préstamo - Solo Admin
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateLoanStatus(string id, [FromBody] UpdateLoanStatusDTO statusDto)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync("WARNING", "ID de préstamo requerido para actualizar estado");
                    return BadRequest("ID del préstamo es requerido");
                }

                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync("WARNING", $"Datos inválidos para actualizar estado del préstamo {id}");
                    return BadRequest(ModelState);
                }

                var username = User.Identity?.Name ?? "Unknown";
                var success = await _loanService.UpdateLoanStatusAsync(id, statusDto.Status);

                if (!success)
                {
                    await _logService.LogAsync("WARNING", $"No se pudo actualizar el estado del préstamo: {id}");
                    return NotFound("Préstamo no encontrado");
                }

                await _logService.LogAsync("INFORMATION", $"Estado del préstamo actualizado: {id} a {statusDto.Status} por {username}");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", $"Error al actualizar estado del préstamo {id}", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtiene estadísticas de préstamos - Solo Admin y Operator
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<object>> GetLoanStatistics()
        {
            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                await _logService.LogAsync("INFORMATION", $"Usuario {username} consultando estadísticas de préstamos");

                var totalActive = await _loanService.GetTotalActiveLoansAsync();
                var totalOverdue = await _loanService.GetTotalOverdueLoansAsync();

                var statistics = new
                {
                    TotalActiveLoans = totalActive,
                    TotalOverdueLoans = totalOverdue,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync("ERROR", "Error al obtener estadísticas de préstamos", ex);
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    public class UpdateLoanStatusDTO
    {
        public LoanStatus Status { get; set; }
    }
}