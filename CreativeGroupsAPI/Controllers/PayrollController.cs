using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreativeGroupsAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using DocumentFormat.OpenXml.InkML;

namespace CreativeGroupsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PayrollController : ControllerBase
    {
        private readonly PayrollDbContext _context;

        public PayrollController(PayrollDbContext context)
        {
            _context = context;
        }

        // PAYROLL MONTH CRUD
        [HttpGet("payrollmonth")]
        public async Task<ActionResult<IEnumerable<PayrollMonth>>> GetPayrollMonths()
        {
            return await _context.Set<PayrollMonth>().ToListAsync();
        }

        [HttpGet("{companyId}")]
        public async Task<ActionResult<IEnumerable<PayrollMonth>>> GetPayrollMonthsByCompany(int companyId)
        {
            var payrollMonths = await _context.Set<PayrollMonth>()
                .Where(pm => pm.CompanyId == companyId)
                .ToListAsync();
            
            return Ok(payrollMonths);
        }

        [HttpGet("payrollmonth/{id}")]
        public async Task<ActionResult<PayrollMonth>> GetPayrollMonth(int id)
        {
            var month = await _context.Set<PayrollMonth>().FindAsync(id);
            if (month == null) return NotFound();
            return month;
        }

        [HttpPost("payrollmonth")]
        public async Task<ActionResult<PayrollMonth>> CreatePayrollMonth(CreatePayrollMonthRequest request)
        {
            if (request == null)
                return BadRequest("No payroll month data provided.");

            var payrollMonth = new PayrollMonth
            {
                CompanyId = request.CompanyId,
                Month = request.Month,
                TotalDays = request.TotalDays
            };

            _context.Set<PayrollMonth>().Add(payrollMonth);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPayrollMonth), new { id = payrollMonth.Id }, payrollMonth);
        }

        [HttpPut("payrollmonth/{id}")]
        public async Task<IActionResult> UpdatePayrollMonth(int id, UpdatePayrollMonthRequest request)
        {
            if (id != request.Id) 
                return BadRequest("PayrollMonth ID mismatch.");

            var existing = await _context.Set<PayrollMonth>().FindAsync(id);
            if (existing == null)
                return NotFound($"PayrollMonth with ID {id} not found.");

            // Update the existing payroll month's fields
            existing.CompanyId = request.CompanyId;
            existing.Month = request.Month;
            existing.TotalDays = request.TotalDays; // Add this missing line

            await _context.SaveChangesAsync();
            return NoContent();
        }

        public class CreatePayrollMonthRequest
        {
            public int CompanyId { get; set; }
            public string Month { get; set; }
            public int TotalDays { get; set; } = 30;
        }

        public class UpdatePayrollMonthRequest
        {
            public int Id { get; set; }
            public int CompanyId { get; set; }
            public string Month { get; set; }
            public int TotalDays { get; set; } = 30;
        }

        [HttpDelete("payrollmonth/{id}")]
        public async Task<IActionResult> DeletePayrollMonth(int id)
        {
            var month = await _context.Set<PayrollMonth>().FindAsync(id);
            if (month == null) return NotFound();
            
            _context.Set<PayrollMonth>().Remove(month);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    
        private int GetTotalDaysInMonth(string monthYear)
        {
            if (DateTime.TryParse($"1 {monthYear}", out var date))
            {
                return DateTime.DaysInMonth(date.Year, date.Month);
            }
            return 30; // Default fallback
        }

        private string ValidateRow(Payroll row, int companyId, string payrollMonth, List<Payroll> allRows)
        {
            // PF/ESI must match an employee
            var emp = _context.Employee.FirstOrDefault(e => e.CompanyId == companyId);
            if (emp == null) return "PF/ESI Number does not match any employee.";
            // Working Days required
            if (row.WorkingDays < 0) return "Working Days required.";
            // BasicDA required
            if (row.BasicDA <= 0) return "Basic + DA required.";
            // Gross Salary required
            if (row.GrossSalary <= 0) return "Gross Salary required.";
            // Reason required if Working Days = 0
            if (row.WorkingDays == 0 && string.IsNullOrWhiteSpace(row.Reason)) return "Reason required when Working Days = 0.";
            // Each employee must appear once per month
            //if (allRows.Count(x => x.PFOrESINumber == row.PFOrESINumber) > 1) return "Duplicate employee in upload.";
            return null;
        }

        private int GetEmployeeIdByPFOrESI(string pfOrEsi, int companyId)
        {
            var emp = _context.Employee.FirstOrDefault(e => e.CompanyId == companyId && (e.PFNumber == pfOrEsi || e.ESINumber == pfOrEsi));
            return emp?.Id ?? 0;
        }
    }
}
