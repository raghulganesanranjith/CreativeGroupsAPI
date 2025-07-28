using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreativeGroupsAPI.Models;

namespace CreativeGroupsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeedController : ControllerBase
    {
        private readonly PayrollDbContext _context;

        public SeedController(PayrollDbContext context)
        {
            _context = context;
        }

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdminUser()
        {
            // Check if admin already exists
            var existingAdmin = await _context.User
                .AnyAsync(u => u.Role == UserRole.Admin);

            if (existingAdmin)
                return BadRequest("Admin user already exists.");

            var admin = new User
            {
                Username = "admin",
                Password = "admin123",
                Role = UserRole.Admin,
                OrganizationId = null,
                IsActive = true
            };

            _context.User.Add(admin);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Admin user created successfully", username = "admin", password = "admin123" });
        }

        [HttpPost("seed-all")]
        public async Task<IActionResult> SeedAllData()
        {
            try
            {
                // Check if admin already exists
                var existingAdmin = await _context.User
                    .AnyAsync(u => u.Role == UserRole.Admin);

                if (!existingAdmin)
                {
                    var admin = new User
                    {
                        Username = "admin",
                        Password = "admin123",
                        Role = UserRole.Admin,
                        OrganizationId = null,
                        IsActive = true
                    };
                    _context.User.Add(admin);
                }

                // Create sample organization if it doesn't exist
                var existingOrg = await _context.Organization
                    .AnyAsync(o => o.Username == "org1");

                if (!existingOrg)
                {
                    var organization = new Organization
                    {
                        Username = "org1",
                        Password = "org123",
                        Name = "Sample Organization",
                        IsActive = true
                    };
                    _context.Organization.Add(organization);
                    await _context.SaveChangesAsync(); // Save to get the organization ID

                    // Create a sample user for this organization
                    var user = new User
                    {
                        Username = "user1",
                        Password = "user123",
                        Role = UserRole.User,
                        OrganizationId = organization.Id,
                        IsActive = true
                    };
                    _context.User.Add(user);
                }

                await _context.SaveChangesAsync();

                return Ok(new { 
                    message = "Seed data created successfully",
                    admin = new { username = "admin", password = "admin123" },
                    organization = new { username = "org1", password = "org123" },
                    user = new { username = "user1", password = "user123" }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error seeding data: {ex.Message}");
            }
        }
    }
}
