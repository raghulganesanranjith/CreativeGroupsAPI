using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreativeGroupsAPI.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeGroupsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly PayrollDbContext _context;

        public UserController(PayrollDbContext context)
        {
            _context = context;
        }

        // GET: api/User (Admin and Organization can see their users)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers([FromQuery] int? organizationId = null)
        {
            var query = _context.User
                .Include(u => u.Organization)
                .Where(u => u.IsActive == true);

            // Filter by organization if provided
            if (organizationId.HasValue)
            {
                query = query.Where(u => u.OrganizationId == organizationId.Value);
            }

            var users = await query
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    Role = u.Role.ToString(),
                    u.OrganizationId,
                    OrganizationName = u.Organization != null ? u.Organization.Name : null,
                    u.IsActive,
                    u.CreatedDate
                })
                .OrderBy(u => u.Username)
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/User/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetUser(int id)
        {
            var user = await _context.User
                .Include(u => u.Organization)
                .Where(u => u.Id == id && u.IsActive == true)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    Role = u.Role.ToString(),
                    u.OrganizationId,
                    OrganizationName = u.Organization != null ? u.Organization.Name : null,
                    u.IsActive,
                    u.CreatedDate
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            return user;
        }

        // POST: api/User (Admin and Organization can create users)
        [HttpPost]
        public async Task<ActionResult<User>> CreateUser([FromBody] CreateUserRequest request)
        {
            if (request == null)
                return BadRequest("No user data provided.");

            // Check if username already exists across all entities (regardless of IsActive status)
            var existingUser = await _context.User
                .AnyAsync(u => u.Username == request.Username);
            
            var existingOrg = await _context.Organization
                .AnyAsync(o => o.Username == request.Username);

            if (existingUser || existingOrg)
                return Conflict("Username already exists. Please choose a different username.");

            // Validate role and organization
            if (request.Role == UserRole.Organization && request.OrganizationId.HasValue)
                return BadRequest("Organization role users cannot be assigned to an organization.");

            if (request.Role == UserRole.User && !request.OrganizationId.HasValue)
                return BadRequest("User role requires an organization assignment.");

            var user = new User
            {
                Username = request.Username,
                Password = request.Password,
                Role = request.Role,
                OrganizationId = request.OrganizationId,
                IsActive = true
            };

            _context.User.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        // PUT: api/User/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var user = await _context.User.FindAsync(id);
            if (user == null)
                return NotFound();

            // Check if username already exists across all entities (excluding current user, regardless of IsActive status)
            var existingUser = await _context.User
                .AnyAsync(u => u.Username == request.Username && u.Id != id);
            
            var existingOrg = await _context.Organization
                .AnyAsync(o => o.Username == request.Username);

            if (existingUser || existingOrg)
                return Conflict("Username already exists. Please choose a different username.");

            // Validate role and organization
            if (request.Role == UserRole.Organization && request.OrganizationId.HasValue)
                return BadRequest("Organization role users cannot be assigned to an organization.");

            if (request.Role == UserRole.User && !request.OrganizationId.HasValue)
                return BadRequest("User role requires an organization assignment.");

            user.Username = request.Username;
            user.Password = request.Password;
            user.Role = request.Role;
            user.OrganizationId = request.OrganizationId;
            user.IsActive = request.IsActive;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/User/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.User.FindAsync(id);
            if (user == null)
                return NotFound();

            // Soft delete
            user.IsActive = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateUserRequest
    {
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; }
        
        [Required]
        [JsonConverter(typeof(UserRoleJsonConverter))]
        public UserRole Role { get; set; }
        
        public int? OrganizationId { get; set; }
    }

    public class UpdateUserRequest
    {
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; }
        
        [Required]
        [JsonConverter(typeof(UserRoleJsonConverter))]
        public UserRole Role { get; set; }
        
        public int? OrganizationId { get; set; }
        
        public bool IsActive { get; set; } = true;
    }

    public class UserRoleJsonConverter : JsonConverter<UserRole>
    {
        public override UserRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (Enum.TryParse<UserRole>(stringValue, true, out var enumValue))
                {
                    return enumValue;
                }
                throw new JsonException($"Unable to convert \"{stringValue}\" to UserRole.");
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                var intValue = reader.GetInt32();
                if (Enum.IsDefined(typeof(UserRole), intValue))
                {
                    return (UserRole)intValue;
                }
                throw new JsonException($"Unable to convert {intValue} to UserRole.");
            }
            
            throw new JsonException($"Unexpected token type {reader.TokenType} when parsing UserRole.");
        }

        public override void Write(Utf8JsonWriter writer, UserRole value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
