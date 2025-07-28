using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreativeGroupsAPI.Models;
using System.ComponentModel.DataAnnotations;

namespace CreativeGroupsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrganizationController : ControllerBase
    {
        private readonly PayrollDbContext _context;

        public OrganizationController(PayrollDbContext context)
        {
            _context = context;
        }

        // GET: api/Organization (Admin only)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Organization>>> GetOrganizations()
        {
            return await _context.Organization
                .Where(o => o.IsActive == true)
                .OrderBy(o => o.Name)
                .ToListAsync();
        }

        // GET: api/Organization/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Organization>> GetOrganization(int id)
        {
            var organization = await _context.Organization.FindAsync(id);
            if (organization == null || !organization.IsActive)
                return NotFound();

            return organization;
        }

        // POST: api/Organization (Admin only)
        [HttpPost]
        public async Task<ActionResult<Organization>> CreateOrganization([FromBody] CreateOrganizationRequest request)
        {
            if (request == null)
                return BadRequest("No organization data provided.");

            // Check if username already exists across all entities (regardless of IsActive status)
            var existingUser = await _context.User
                .AnyAsync(u => u.Username == request.Username);
            
            var existingOrg = await _context.Organization
                .AnyAsync(o => o.Username == request.Username);

            if (existingUser || existingOrg)
                return Conflict("Username already exists. Please choose a different username.");

            var organization = new Organization
            {
                Name = request.Name,
                Username = request.Username,
                Password = request.Password,
                IsActive = true
            };

            _context.Organization.Add(organization);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrganization), new { id = organization.Id }, organization);
        }

        // PUT: api/Organization/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrganization(int id, [FromBody] UpdateOrganizationRequest request)
        {
            var organization = await _context.Organization.FindAsync(id);
            if (organization == null)
                return NotFound();

            // Check if username already exists across all entities (excluding current organization, regardless of IsActive status)
            var existingUser = await _context.User
                .AnyAsync(u => u.Username == request.Username);
            
            var existingOrg = await _context.Organization
                .AnyAsync(o => o.Username == request.Username && o.Id != id);

            if (existingUser || existingOrg)
                return Conflict("Username already exists. Please choose a different username.");

            organization.Name = request.Name;
            organization.Username = request.Username;
            organization.Password = request.Password;
            organization.IsActive = request.IsActive;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Organization/{id} (Admin only)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrganization(int id)
        {
            var organization = await _context.Organization.FindAsync(id);
            if (organization == null)
                return NotFound();

            // Soft delete
            organization.IsActive = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateOrganizationRequest
    {
        [Required]
        public string Name { get; set; }
        
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; }
    }

    public class UpdateOrganizationRequest
    {
        [Required]
        public string Name { get; set; }
        
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
}
