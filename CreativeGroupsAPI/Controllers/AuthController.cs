using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreativeGroupsAPI.Models;
using System.ComponentModel.DataAnnotations;

namespace CreativeGroupsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PayrollDbContext _context;

        public AuthController(PayrollDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest("Invalid login request.");

            // First check if it's an Admin login
            var user = await _context.User
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Username == request.Username && 
                                        u.Password == request.Password && 
                                        u.IsActive == true);

            if (user != null)
            {
                return Ok(new LoginResponse
                {
                    Success = true,
                    UserId = user.Id,
                    Username = user.Username,
                    Role = user.Role.ToString(),
                    OrganizationId = user.OrganizationId,
                    OrganizationName = user.Organization?.Name
                });
            }

            // Check if it's an Organization login
            var organization = await _context.Organization
                .FirstOrDefaultAsync(o => o.Username == request.Username && 
                                         o.Password == request.Password && 
                                         o.IsActive == true);

            if (organization != null)
            {
                return Ok(new LoginResponse
                {
                    Success = true,
                    UserId = organization.Id,
                    Username = organization.Username,
                    Role = "Organization",
                    OrganizationId = organization.Id,
                    OrganizationName = organization.Name
                });
            }

            return Unauthorized(new LoginResponse
            {
                Success = false,
                Message = "Invalid username or password."
            });
        }
    }

    public class LoginRequest
    {
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
        public int? OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public string Message { get; set; }
    }
}
