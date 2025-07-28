       
       

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreativeGroupsAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CreativeGroupsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeController : ControllerBase
    {
        private readonly PayrollDbContext _context;
        public EmployeeController(PayrollDbContext context) { _context = context; }

        // DELETE: api/Employee/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployeebyID(int id)
        {
            var emp = await _context.Employee.FindAsync(id);
            if (emp == null)
                return NotFound();
            
            // Store the company ID before deleting the employee
            int companyId = emp.CompanyId;
            
            // First delete all related PayrollEntry records
            var relatedPayrollEntries = await _context.PayrollEntry
                .Where(pe => pe.EmployeeId == id)
                .ToListAsync();
            
            if (relatedPayrollEntries.Any())
            {
                _context.PayrollEntry.RemoveRange(relatedPayrollEntries);
            }
            
            // Then delete the employee
            _context.Employee.Remove(emp);
            await _context.SaveChangesAsync();

            // After deleting, validate all remaining employees in the company to update error status
            var allEmployeesInCompany = await _context.Employee
                .Where(e => e.CompanyId == companyId)
                .ToListAsync();

            // Validate all remaining employees and update their error status
            foreach (var employee in allEmployeesInCompany)
            {
                employee.Error = ValidateRow(employee, employee.CompanyId, allEmployeesInCompany);
            }

            // Save the updated error statuses
            if (allEmployeesInCompany.Any())
            {
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }
        // POST: api/Employee
        [HttpPost]
        public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeRequest request)
        {
            if (request == null)
                return BadRequest("No employee data provided.");
            
            // Create new employee from request
            var employee = new Employee
            {
                CompanyId = request.CompanyId,
                Name = request.Name,
                JoiningDate = request.JoiningDate,
                LeavingDate = request.LeavingDate,
                PFNumber = request.PFNumber,
                ESINumber = request.ESINumber,
                IsActive = request.IsActive
            };
            
            // Optionally validate uniqueness (Name + PFNumber + ESINumber + CompanyId)
            bool exists = _context.Employee.Any(e =>
                e.CompanyId == employee.CompanyId &&
                e.Name == employee.Name &&
                e.PFNumber == employee.PFNumber &&
                e.ESINumber == employee.ESINumber);
            if (exists)
                return Conflict("Employee already exists.");
            
            _context.Employee.Add(employee);
            await _context.SaveChangesAsync();

            // After saving changes, validate all employees in the company to update error status
            var allEmployeesInCompany = await _context.Employee
                .Where(e => e.CompanyId == employee.CompanyId)
                .ToListAsync();

            // Validate all employees and update their error status
            foreach (var emp in allEmployeesInCompany)
            {
                emp.Error = ValidateRow(emp, emp.CompanyId, allEmployeesInCompany);
            }

            // Save the updated error statuses
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEmployee), new { companyId = employee.CompanyId }, employee);
        }

        public class CreateEmployeeRequest
        {
            public int CompanyId { get; set; }
            public string Name { get; set; }
            public DateTime JoiningDate { get; set; }
            public DateTime? LeavingDate { get; set; }
            public string PFNumber { get; set; }
            public string ESINumber { get; set; }
            public bool IsActive { get; set; } = true;
        }
        // POST: api/Employee/upload
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] EmployeeUpload Model)
        {
            try
            {
                var file = Model.formFile;
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded.");
                var rows = new List<Employee>();
                using (var stream = file.OpenReadStream())
                using (var workbook = new ClosedXML.Excel.XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheets.First();
                    Dictionary<string, int> colMap = new();
                    int headerRowNum = 1;
                    void ReadHeaderRow(int rowNum)
                    {
                        colMap.Clear();
                        var headerRow = worksheet.Row(rowNum);
                        for (int col = 1; col <= worksheet.LastColumnUsed().ColumnNumber(); col++)
                        {
                            var colName = headerRow.Cell(col).GetString().Trim();
                            if (!string.IsNullOrEmpty(colName) && !colMap.ContainsKey(colName.ToLower()))
                                colMap[colName.ToLower()] = col;
                        }
                    }
                    ReadHeaderRow(1);
                    // Helper to find column index by exact name matching
                    int FindCol(string exactName) => colMap.TryGetValue(exactName.ToLower(), out var colIndex) ? colIndex : 0;
                    
                    // Map columns with exact name matching
                    int colName = FindCol("name");
                    int colJoiningDate = FindCol("joining_date");
                    int colPF = FindCol("pf");
                    int colESI = FindCol("esi");
                    int colLeavingDate = FindCol("leaving_date"); // Optional column
                    
                    var missingCols = new List<string>();
                    if (colName == 0) missingCols.Add("name");
                    if (colJoiningDate == 0) missingCols.Add("joining_date");
                    if (colPF == 0) missingCols.Add("pf");
                    if (colESI == 0) missingCols.Add("esi");
                    // Note: leaving_date is optional, so no validation required
                    
                    // If not all required columns found, try row 2 as header
                    if (missingCols.Count > 0)
                    {
                        ReadHeaderRow(2);
                        missingCols.Clear();
                        
                        // Re-map columns with exact names
                        colName = FindCol("name");
                        colJoiningDate = FindCol("joining_date");
                        colPF = FindCol("pf");
                        colESI = FindCol("esi");
                        colLeavingDate = FindCol("leaving_date"); // Optional column
                        
                        if (colName == 0) missingCols.Add("name");
                        if (colJoiningDate == 0) missingCols.Add("joining_date");
                        if (colPF == 0) missingCols.Add("pf");
                        if (colESI == 0) missingCols.Add("esi");
                        // Note: leaving_date is optional, so no validation required
                        
                        headerRowNum = 2;
                    }
                    if (missingCols.Count > 0)
                        return BadRequest($"Missing required column(s): {string.Join(", ", missingCols)}");
                    int rowNum = headerRowNum + 1;
                    while (true)
                    {
                        var name = worksheet.Cell(rowNum, colName).GetString();
                        var joiningDateStr = worksheet.Cell(rowNum, colJoiningDate).GetString();
                        var pfNumber = worksheet.Cell(rowNum, colPF).GetString();
                        var esiNumber = worksheet.Cell(rowNum, colESI).GetString();
                        var leavingDateStr = colLeavingDate > 0 ? worksheet.Cell(rowNum, colLeavingDate).GetString() : "";
                        
                        if (string.IsNullOrWhiteSpace(name)) break;
                        
                        DateTime? joiningDate = null;
                        if (DateTime.TryParse(joiningDateStr, out var jdt)) joiningDate = jdt;
                        
                        DateTime? leavingDate = null;
                        if (!string.IsNullOrWhiteSpace(leavingDateStr) && DateTime.TryParse(leavingDateStr, out var ldt)) 
                            leavingDate = ldt;
                        
                        rows.Add(new Employee
                        {
                            Name = name,
                            JoiningDate = joiningDate ?? default(DateTime),
                            PFNumber = pfNumber,
                            ESINumber = esiNumber,
                            LeavingDate = leavingDate, // Optional field
                            CompanyId = Model.CompanyId
                        });
                        rowNum++;
                    }
                }
                // Validate and add only new Employees (ignore duplicates)
                foreach (var row in rows)
                {
                    row.Error = ValidateRow(row, row.CompanyId, rows);
                    // Check for duplicate in DB (by Name + PFNumber + ESINumber + CompanyId)
                    bool exists = _context.Employee.Any(e =>
                        e.CompanyId == row.CompanyId &&
                        e.Name == row.Name &&
                        e.PFNumber == row.PFNumber &&
                        e.ESINumber == row.ESINumber);
                    if (!exists)
                    {
                        _context.Employee.Add(row);
                    }
                    // else: skip duplicate
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing file: {ex.Message}");
            }
            return Ok();
        }


        // POST: api/Employee/Update
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] List<Employee> fixes)
        {
            if (fixes == null || fixes.Count == 0)
                return BadRequest("No fixes provided.");

            foreach (var fix in fixes)
            {
                // Find existing employee by ID for direct updates
                var existing = await _context.Employee.FindAsync(fix.Id);
                
                if (existing != null)
                {
                    // Update the existing employee's fields
                    existing.Name = fix.Name;
                    existing.JoiningDate = fix.JoiningDate;
                    existing.LeavingDate = fix.LeavingDate;
                    existing.PFNumber = fix.PFNumber;
                    existing.ESINumber = fix.ESINumber;
                    existing.CompanyId = fix.CompanyId; // In case company is changed
                    
                    // Validate the updated employee
                    existing.Error = ValidateRow(existing, existing.CompanyId, new List<Employee> { existing });
                    
                    // Mark as modified
                    _context.Entry(existing).State = EntityState.Modified;
                }
                else
                {
                    return NotFound($"Employee with ID {fix.Id} not found.");
                }
            }
            
            await _context.SaveChangesAsync();
            
            // Return the updated employees for the company
            var companyId = fixes.First().CompanyId;
            var updated = await _context.Employee.Where(x => x.CompanyId == companyId).ToListAsync();
            return Ok(updated);
        }

        // PUT: api/Employee/{id} - Update a single employee by ID
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(int id, [FromBody] UpdateEmployeeRequest request)
        {
            if (id != request.Id)
                return BadRequest("Employee ID mismatch.");

            var existing = await _context.Employee.FindAsync(id);
            if (existing == null)
                return NotFound($"Employee with ID {id} not found.");

            // Update the existing employee's fields
            existing.Name = request.Name;
            existing.JoiningDate = request.JoiningDate;
            existing.LeavingDate = request.LeavingDate;
            existing.PFNumber = request.PFNumber;
            existing.ESINumber = request.ESINumber;
            existing.CompanyId = request.CompanyId;
            existing.IsActive = request.IsActive;

            try
            {
                await _context.SaveChangesAsync();

                // After saving changes, validate all employees in the company to update error status
                var allEmployeesInCompany = await _context.Employee
                    .Where(e => e.CompanyId == existing.CompanyId)
                    .ToListAsync();

                // Validate all employees and update their error status
                foreach (var employee in allEmployeesInCompany)
                {
                    employee.Error = ValidateRow(employee, employee.CompanyId, allEmployeesInCompany);
                }

                // Save the updated error statuses
                await _context.SaveChangesAsync();

                return Ok(existing);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(id))
                    return NotFound();
                throw;
            }
        }

        public class UpdateEmployeeRequest
        {
            public int Id { get; set; }
            public int CompanyId { get; set; }
            public string Name { get; set; }
            public DateTime JoiningDate { get; set; }
            public DateTime? LeavingDate { get; set; }
            public string PFNumber { get; set; }
            public string ESINumber { get; set; }
            public bool IsActive { get; set; } = true;
        }

        private bool EmployeeExists(int id)
        {
            return _context.Employee.Any(e => e.Id == id);
        }


        // GET: api/Employee/{companyId}
        [HttpGet("{companyId}")]
        public async Task<ActionResult<IEnumerable<Employee>>> GetEmployee(int companyId, 
            [FromQuery] string? searchName = null, 
            [FromQuery] string? searchPF = null, 
            [FromQuery] string? searchESI = null)
        {
            var query = _context.Employee.Where(x => x.CompanyId == companyId);
            
            // Apply search filters if provided
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(e => e.Name.ToLower().Contains(searchName.ToLower()));
            }
            
            if (!string.IsNullOrWhiteSpace(searchPF))
            {
                query = query.Where(e => e.PFNumber != null && e.PFNumber.ToLower().Contains(searchPF.ToLower()));
            }
            
            if (!string.IsNullOrWhiteSpace(searchESI))
            {
                query = query.Where(e => e.ESINumber != null && e.ESINumber.ToLower().Contains(searchESI.ToLower()));
            }
            
            return await query.OrderBy(e => e.Name).ToListAsync();
        }

        // DELETE: api/Employee/company/{companyId} - Delete all employees for a company
        [HttpDelete("company/{companyId}")]
        public async Task<IActionResult> DeleteAllEmployeesByCompany(int companyId)
        {
            var employees = await _context.Employee.Where(e => e.CompanyId == companyId).ToListAsync();
            
            if (!employees.Any())
                return NotFound($"No employees found for company ID {companyId}.");

            // First delete all related PayrollEntry records for these employees
            var employeeIds = employees.Select(e => e.Id).ToList();
            var relatedPayrollEntries = await _context.PayrollEntry
                .Where(pe => employeeIds.Contains(pe.EmployeeId))
                .ToListAsync();
            
            if (relatedPayrollEntries.Any())
            {
                _context.PayrollEntry.RemoveRange(relatedPayrollEntries);
            }

            // Then delete all employees
            _context.Employee.RemoveRange(employees);
            await _context.SaveChangesAsync();

            return Ok($"Successfully deleted {employees.Count} employees for company ID {companyId}.");
        }


        // No commit endpoint needed; upload and fixes directly update Employee


        //// DELETE: api/Employee/{companyId}
        //[HttpDelete("{companyId}")]
        //public async Task<IActionResult> DeleteEmployeebyCompanyID(int companyId)
        //{
        //    var old = _context.Employee.Where(x => x.CompanyId == companyId);
        //    _context.Employee.RemoveRange(old);
        //    await _context.SaveChangesAsync();
        //    return NoContent();
        //}


        // GET: api/Employee/has-errors/{companyId}
        [HttpGet("has-errors/{companyId}")]
        public async Task<ActionResult<bool>> HasErrors(int companyId)
        {
            var allRows = await _context.Employee
                .Where(x => x.CompanyId == companyId)
                .ToListAsync();
            var errorRows = allRows.Where(x => !string.IsNullOrEmpty(x.Error)).ToList();
            var hasErrors = errorRows.Count > 0;
            var resultRows = errorRows.Concat(allRows.Where(x => string.IsNullOrEmpty(x.Error))).ToList();
            return Ok(new { rows = resultRows });
        }


        private string ValidateRow(Employee row, int companyId, List<Employee> allRows)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) return "Name is required.";
            var company = _context.Company.Find(companyId);
            if (company == null) return "Invalid company.";
            if (company.PFEnabled && string.IsNullOrWhiteSpace(row.PFNumber)) return "PF Number required.";
            bool esiIsNil = !string.IsNullOrWhiteSpace(row.ESINumber) && row.ESINumber.Trim().Equals("NIL", StringComparison.OrdinalIgnoreCase);
            if (company.ESIEnabled && string.IsNullOrWhiteSpace(row.ESINumber) && !esiIsNil) return "ESI Number required.";
            
            // Check for duplicates in the current batch, excluding the current row
            if (!string.IsNullOrWhiteSpace(row.PFNumber) && allRows.Count(x => x.PFNumber == row.PFNumber && x.Id != row.Id) > 0)
                return "Duplicate PF Number in upload.";
            if (!esiIsNil && !string.IsNullOrWhiteSpace(row.ESINumber) && allRows.Count(x => x.ESINumber == row.ESINumber && x.Id != row.Id) > 0)
                return "Duplicate ESI Number in upload.";
            
            // Check for duplicates in the database, excluding the current row
            if (!string.IsNullOrWhiteSpace(row.PFNumber) && _context.Employee.Any(x => x.PFNumber == row.PFNumber && x.CompanyId == companyId && x.Id != row.Id))
                return "Duplicate PF Number in database.";
            if (!esiIsNil && !string.IsNullOrWhiteSpace(row.ESINumber) && _context.Employee.Any(x => x.ESINumber == row.ESINumber && x.CompanyId == companyId && x.Id != row.Id))
                return "Duplicate ESI Number in database.";
            
            return null;
        }

        // All staging row endpoints removed; use direct Employee CRUD

    }
}
