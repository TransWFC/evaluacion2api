using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using LibraryApp.Models;
using LibraryApp.Services;
using System.Security.Claims;
using Microsoft.IdentityModel.Logging;
using Microsoft.AspNetCore.RateLimiting;


namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación para todos los endpoints

    public class LoansController : ControllerBase
    {
        private readonly LoanService _loanService;
        private readonly Logservice _logService;

        private const string LogError = "ERROR";
        private const string LogWarning = "WARNING";
        private const string LogInfo = "INFORMATION";
        private const string ErrInternalServer = "Error interno del servidor";
        private const string UnknownValue = "Unknown";
        private const string LoanNotFound = "Prestamo no encontrado";
        private const string BadRequestIdRequired = "ID Del préstamo requerido";

        public LoansController(LoanService loanService, Logservice logService)
        {
            _loanService = loanService;
            _logService = logService;
        }

        // Elimina un préstamo. 

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        [EnableRateLimiting("WritePolicy")]
        public async Task<IActionResult> DeleteLoan(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                await _logService.LogAsync(LogWarning, "ID de préstamo requerido para eliminar");
                return BadRequest(BadRequestIdRequired);
            }

            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) solicitó eliminar préstamo {id}");

                var ok = await _loanService.DeleteLoanAsync(id);

                if (!ok)
                {
                    await _logService.LogAsync(LogWarning, $"No se pudo eliminar el préstamo {id} (no existe o fallo)");
                    return NotFound(LoanNotFound);
                }

                await _logService.LogAsync(LogWarning, $"Préstamo {id} eliminado correctamente");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al eliminar préstamo {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Actualiza campos 
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        [EnableRateLimiting("WritePolicy")]
        public async Task<IActionResult> UpdateLoan(string id, [FromBody] UpdateLoanDTO dto)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                await _logService.LogAsync(LogWarning, "ID de préstamo requerido para actualizar");
                return BadRequest(BadRequestIdRequired);
            }

            if (!ModelState.IsValid)
            {
                await _logService.LogAsync(LogWarning, $"Datos inválidos para actualizar préstamo {id}");
                return BadRequest(ModelState);
            }

            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) actualizando préstamo {id}");

                var ok = await _loanService.UpdateLoanAsync(id, dto, $"{username} ({role})");
                if (!ok)
                {
                    await _logService.LogAsync(LogWarning, $"No se pudo actualizar el préstamo {id}");
                    return NotFound(LoanNotFound);
                }

                await _logService.LogAsync(LogInfo, $"Préstamo {id} actualizado correctamente");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al actualizar préstamo {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,Bibliotecario")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetAllLoans()
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) consultando todos los préstamos");

                var loans = await _loanService.GetAllLoansAsync();
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, "Error al obtener préstamos", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Obtiene préstamos del usuario autenticado
        [HttpGet("my-loans")]
        [Authorize] // Todos los usuarios autenticados pueden ver sus propios préstamos
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetMyLoans()
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) consultando sus préstamos");

                var loans = await _loanService.GetLoansByUserAsync(username);
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al obtener préstamos del usuario {User.Identity?.Name}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Obtiene préstamos activos del usuario autenticado
        [HttpGet("my-active-loans")]
        [Authorize]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetMyActiveLoans()
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) consultando sus préstamos activos");

                var loans = await _loanService.GetActiveLoansByUserAsync(username);
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al obtener préstamos activos del usuario {User.Identity?.Name}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// <summary>
        /// Obtiene préstamos vencidos - Solo Administrador y Bibliotecario
        /// </summary>
        [HttpGet("overdue")]
        [Authorize(Roles = "Administrador,Bibliotecario")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetOverdueLoans()
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo, $"Usuario {username} (Rol: {role}) consultando préstamos vencidos");

                var loans = await _loanService.GetOverdueLoansAsync();
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, "Error al obtener préstamos vencidos", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Crea un nuevo préstamo 
        [HttpPost]
        [Authorize(Roles = "Administrador,Bibliotecario")]
        [EnableRateLimiting("WritePolicy")]
        public async Task<ActionResult<LoanResponseDTO>> CreateLoan([FromBody] LoanRequestDTO loanRequest)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync(LogWarning, "Datos inválidos para crear préstamo");
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(loanRequest.BookId))
                {
                    await _logService.LogAsync(LogWarning, "ID del libro es requerido para crear préstamo");
                    return BadRequest("ID del libro es requerido");
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                var processedBy = $"{username} ({role})";


                var loanForUser = !string.IsNullOrEmpty(loanRequest.Username) ? loanRequest.Username : username;

                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) creando préstamo del libro {loanRequest.BookId} para usuario {loanForUser}");

                var loan = await _loanService.CreateLoanAsync(loanRequest, loanForUser, processedBy);

                if (loan == null)
                {
                    await _logService.LogAsync(LogWarning, $"No se pudo crear el préstamo para el libro: {loanRequest.BookId}");
                    return BadRequest("No se pudo crear el préstamo. Verifique la disponibilidad del libro y los préstamos activos del usuario.");
                }

                await _logService.LogAsync(LogInfo, $"Préstamo creado exitosamente: {loan.Id} por {username} (Rol: {role})");
                return CreatedAtAction(nameof(GetMyLoans), new { id = loan.Id }, loan);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al crear préstamo para libro: {loanRequest?.BookId}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        // obtener un préstamo específico por ID
        [HttpGet("{id:length(24)}")]
        [Authorize] 
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<LoanResponseDTO>> GetById(string id)
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) solicitó préstamo {id}");

                var loan = await _loanService.GetLoanByIdAsync(id);
                if (loan == null)
                {
                    await _logService.LogAsync(LogWarning, $"Préstamo no encontrado: {id}");
                    return NotFound(LoanNotFound);
                }

                // Admin/Bibliotecario son privilegiados y pueden ver préstamo a detalle por id
                var isPrivileged = User.IsInRole("Administrador") || User.IsInRole("Bibliotecario");
                if (!isPrivileged &&
                    !string.Equals(loan.Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    await _logService.LogAsync(LogWarning,
                        $"Acceso denegado a préstamo {id} para usuario {username}");
                    return Forbid();
                }

                return Ok(loan);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al obtener préstamo {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Solicita un préstamo
        [HttpPost("request")]
        [Authorize] // Todos los usuarios autenticados pueden solicitar 
        [EnableRateLimiting("WritePolicy")]
        public async Task<ActionResult<LoanResponseDTO>> RequestLoan([FromBody] LoanRequestDTO loanRequest)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync(LogWarning, "Datos inválidos para solicitar préstamo");
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(loanRequest.BookId))
                {
                    await _logService.LogAsync(LogWarning, "ID del libro es requerido para solicitar préstamo");
                    return BadRequest("ID del libro es requerido");
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                var processedBy = $"Auto-aprobado ({role})";

                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) solicitando préstamo del libro {loanRequest.BookId}");

                var loan = await _loanService.CreateLoanAsync(loanRequest, username, processedBy);

                if (loan == null)
                {
                    await _logService.LogAsync(LogWarning,
                        $"No se pudo crear la solicitud de préstamo para el libro: {loanRequest.BookId}");
                    return BadRequest("No se pudo procesar la solicitud. Verifique la disponibilidad del libro y sus préstamos activos.");
                }

                await _logService.LogAsync(LogInfo,
                    $"Solicitud de préstamo creada exitosamente: {loan.Id} para usuario {username} (Rol: {role})");
                return Ok(loan);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al solicitar préstamo para libro: {loanRequest?.BookId}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Devuelve un libro 
        [HttpPut("{id}/return")]
        [Authorize(Roles = "Administrador,Bibliotecario")]
        [EnableRateLimiting("WritePolicy")]
        public async Task<IActionResult> ReturnBook(string id, [FromBody] ReturnBookDTO returnDto)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync(LogWarning, "ID de préstamo requerido para devolución");
                    return BadRequest(BadRequestIdRequired);
                }

                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync(LogWarning, $"Datos inválidos para devolver libro del préstamo {id}");
                    return BadRequest(ModelState);
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;

                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) procesando devolución del préstamo {id}");

                var success = await _loanService.ReturnBookAsync(id, returnDto, $"{username} ({role})");

                if (!success)
                {
                    await _logService.LogAsync(LogWarning, $"No se pudo procesar la devolución del préstamo: {id}");
                    return NotFound("Préstamo no encontrado o ya devuelto");
                }

                await _logService.LogAsync(LogInfo,
                    $"Devolución procesada exitosamente: {id} por {username} (Rol: {role})");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al procesar devolución del préstamo {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Actualiza el estado de  préstamo 
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Administrador")]
        [EnableRateLimiting("WritePolicy")]
        public async Task<IActionResult> UpdateLoanStatus(string id, [FromBody] UpdateLoanStatusDto statusDto)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    await _logService.LogAsync(LogWarning, "ID de préstamo requerido para actualizar estado");
                    return BadRequest(BadRequestIdRequired);
                }

                if (!ModelState.IsValid)
                {
                    await _logService.LogAsync(LogWarning, $"Datos inválidos para actualizar estado del préstamo {id}");
                    return BadRequest(ModelState);
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;

                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) actualizando estado del préstamo {id} a {statusDto.Status}");

                var success = await _loanService.UpdateLoanStatusAsync(id, statusDto.Status);

                if (!success)
                {
                    await _logService.LogAsync(LogWarning, $"No se pudo actualizar el estado del préstamo: {id}");
                    return NotFound(LoanNotFound);
                }

                await _logService.LogAsync(LogInfo,
                    $"Estado del préstamo actualizado: {id} a {statusDto.Status} por {username} (Rol: {role})");
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al actualizar estado del préstamo {id}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Obtiene estadísticas de préstamos
        [HttpGet("statistics")]
        [Authorize(Roles = "Administrador,Bibliotecario")]
        [EnableRateLimiting("ReadPolicy")]
        public async Task<ActionResult<object>> GetLoanStatistics()
        {
            try
            {
                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) consultando estadísticas de préstamos");

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
                await _logService.LogAsync(LogError, "Error al obtener estadísticas de préstamos", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }

        /// Obtiene préstamos de un usuario específico
        [HttpGet("user/{targetUsername}")]
        [Authorize(Roles = "Administrador")]
        [EnableRateLimiting("ReadPolicy")]

        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetUserLoans(string targetUsername)
        {
            try
            {
                if (string.IsNullOrEmpty(targetUsername))
                {
                    await _logService.LogAsync(LogWarning, "Username requerido para consultar préstamos");
                    return BadRequest("Username es requerido");
                }

                var username = User.Identity?.Name ?? UnknownValue;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UnknownValue;
                await _logService.LogAsync(LogInfo,
                    $"Usuario {username} (Rol: {role}) consultando préstamos del usuario {targetUsername}");

                var loans = await _loanService.GetLoansByUserAsync(targetUsername);
                return Ok(loans);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(LogError, $"Error al obtener préstamos del usuario {targetUsername}", ex);
                return StatusCode(500, ErrInternalServer);
            }
        }
    }

    public class UpdateLoanStatusDto
    {
        public LoanStatus Status { get; set; }
    }
}