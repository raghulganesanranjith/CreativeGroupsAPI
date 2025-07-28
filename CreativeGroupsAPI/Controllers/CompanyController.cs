using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreativeGroupsAPI.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CreativeGroupsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompanyController : ControllerBase
    {
        private readonly PayrollDbContext _context;
        public CompanyController(PayrollDbContext context) { _context = context; }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Company>>> GetCompany([FromQuery] int? organizationId = null) 
        {
            var query = _context.Company.AsQueryable();
            
            // Filter by organization if provided
            if (organizationId.HasValue)
            {
                query = query.Where(c => c.OrganizationId == organizationId.Value);
            }
            
            return await query.OrderBy(c => c.Name).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Company>> GetCompany(int id)
        {
            var company = await _context.Company.FindAsync(id);
            if (company == null) return NotFound();
            return company;
        }

        [HttpPost]
        public async Task<ActionResult<Company>> CreateCompany(Company company)
        {
            _context.Company.Add(company);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCompany), new { id = company.CompanyId }, company);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCompany(int id, Company company)
        {
            if (id != company.CompanyId) return BadRequest();
            _context.Entry(company).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            var company = await _context.Company.FindAsync(id);
            if (company == null) return NotFound();
            _context.Company.Remove(company);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
