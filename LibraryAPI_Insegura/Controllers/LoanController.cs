// LibraryApp/Controllers/LoansController.cs  (UNSAFE VERSION)
using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using LibraryApp.Services;

namespace LibraryApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // NOTE: No [Authorize] here on purpose (unsafe API)
    public class LoansController : ControllerBase
    {
        private readonly LoanService _loanService;

        public LoansController(LoanService loanService)
        {
            _loanService = loanService;
        }

        // ===== Read =====

        /// <summary>
        /// Get ALL loans (no restrictions).
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetAllLoans()
        {
            var loans = await _loanService.GetAllLoansAsync();
            return Ok(loans);
        }

        /// <summary>
        /// Get "my" loans. In unsafe mode, if ?username is missing, we just return ALL loans.
        /// </summary>
        [HttpGet("my-loans")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetMyLoans([FromQuery] string? username = null)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                // unsafe behavior: show everything
                var all = await _loanService.GetAllLoansAsync();
                return Ok(all);
            }

            var loans = await _loanService.GetLoansByUserAsync(username);
            return Ok(loans);
        }

        /// <summary>
        /// Get "my" ACTIVE loans. In unsafe mode, if ?username is missing, return active loans for EVERYONE.
        /// </summary>
        [HttpGet("my-active-loans")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetMyActiveLoans([FromQuery] string? username = null)
        {
            if (!string.IsNullOrWhiteSpace(username))
            {
                var userActives = await _loanService.GetActiveLoansByUserAsync(username);
                return Ok(userActives);
            }

            // unsafe: active for everyone
            var all = await _loanService.GetAllLoansAsync();
            var active = all.Where(l => l.Status == LoanStatus.Active && l.DueDate > DateTime.MinValue);
            return Ok(active);
        }

        /// <summary>
        /// Get loans by explicit username (no restrictions).
        /// </summary>
        [HttpGet("user/{targetUsername}")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetUserLoans(string targetUsername)
        {
            if (string.IsNullOrWhiteSpace(targetUsername))
                return BadRequest("Username is required");

            var loans = await _loanService.GetLoansByUserAsync(targetUsername);
            return Ok(loans);
        }

        /// <summary>
        /// Get overdue loans (open).
        /// </summary>
        [HttpGet("overdue")]
        public async Task<ActionResult<IEnumerable<LoanResponseDTO>>> GetOverdueLoans()
        {
            var loans = await _loanService.GetOverdueLoansAsync();
            return Ok(loans);
        }

        /// <summary>
        /// Get loan by ID (open).
        /// </summary>
        [HttpGet("{id:length(24)}")]
        public async Task<ActionResult<LoanResponseDTO>> GetById(string id)
        {
            var loan = await _loanService.GetLoanByIdAsync(id);
            if (loan == null) return NotFound("Loan not found");
            return Ok(loan);
        }

        /// <summary>
        /// Public statistics (unsafe).
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetLoanStatistics()
        {
            var totalActive = await _loanService.GetTotalActiveLoansAsync();
            var totalOverdue = await _loanService.GetTotalOverdueLoansAsync();

            var stats = new
            {
                TotalActiveLoans = totalActive,
                TotalOverdueLoans = totalOverdue,
                Timestamp = DateTime.UtcNow
            };
            return Ok(stats);
        }

        // ===== Mutations =====

        /// <summary>
        /// Create loan for any user (unsafe).
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<LoanResponseDTO>> CreateLoan([FromBody] LoanRequestDTO loanRequest)
        {
            if (loanRequest == null) return BadRequest("Request body is required");
            if (string.IsNullOrWhiteSpace(loanRequest.BookId)) return BadRequest("BookId is required");

            // unsafe: caller can set Username; fallback to "Anonymous"
            var targetUser = string.IsNullOrWhiteSpace(loanRequest.Username) ? "Anonymous" : loanRequest.Username;

            var loan = await _loanService.CreateLoanAsync(loanRequest, targetUser, "System");
            if (loan == null) return BadRequest("Could not create loan");
            return Ok(loan);
        }

        /// <summary>
        /// Self-service request (still unsafe; no auth).
        /// </summary>
        [HttpPost("request")]
        public async Task<ActionResult<LoanResponseDTO>> RequestLoan([FromBody] LoanRequestDTO loanRequest)
        {
            if (loanRequest == null) return BadRequest("Request body is required");
            if (string.IsNullOrWhiteSpace(loanRequest.BookId)) return BadRequest("BookId is required");

            // unsafe: if Username omitted, treat as "Anonymous"
            var username = string.IsNullOrWhiteSpace(loanRequest.Username) ? "Anonymous" : loanRequest.Username;

            var loan = await _loanService.CreateLoanAsync(loanRequest, username, "Self-Service");
            if (loan == null) return BadRequest("Could not process request");
            return Ok(loan);
        }

        /// <summary>
        /// Update editable fields (unsafe – anyone).
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLoan(string id, [FromBody] UpdateLoanDTO dto)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Id is required");
            if (dto == null) return BadRequest("Request body is required");

            var ok = await _loanService.UpdateLoanAsync(id, dto, "System");
            if (!ok) return NotFound("Loan not found");
            return NoContent();
        }

        /// <summary>
        /// Mark as returned / lost (unsafe – anyone).
        /// </summary>
        [HttpPut("{id}/return")]
        public async Task<IActionResult> ReturnBook(string id, [FromBody] ReturnBookDTO returnDto)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Id is required");
            if (returnDto == null) return BadRequest("Request body is required");

            // If caller didn’t pass LoanId in body, use route id
            if (string.IsNullOrWhiteSpace(returnDto.LoanId)) returnDto.LoanId = id;

            var ok = await _loanService.ReturnBookAsync(id, returnDto, "System");
            if (!ok) return NotFound("Loan not found or already returned");
            return NoContent();
        }

        /// <summary>
        /// Update loan status by id (unsafe – anyone).
        /// </summary>
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateLoanStatus(string id, [FromBody] UpdateLoanStatusDTO dto)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Id is required");
            if (dto == null) return BadRequest("Request body is required");

            var ok = await _loanService.UpdateLoanStatusAsync(id, dto.Status);
            if (!ok) return NotFound("Loan not found");
            return NoContent();
        }

        /// <summary>
        /// Delete loan (unsafe – anyone).
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLoan(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Id is required");

            var ok = await _loanService.DeleteLoanAsync(id);
            if (!ok) return NotFound("Loan not found");
            return NoContent();
        }
    }

    // Keep here if not already in your Models folder
    public class UpdateLoanStatusDTO
    {
        public LoanStatus Status { get; set; }
    }
}
