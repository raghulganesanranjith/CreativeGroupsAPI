using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreativeGroupsAPI.Models;
using ClosedXML.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace CreativeGroupsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PayrollUploadController : ControllerBase
    {
        private readonly PayrollDbContext _context;

        public PayrollUploadController(PayrollDbContext context)
        {
            _context = context;
        }

        // Check if employee master has no errors before allowing payroll upload
        [HttpGet("can-upload/{companyId}")]
        public async Task<IActionResult> CanUploadPayroll(int companyId)
        {
            var hasEmployeeErrors = await _context.Employee
                .AnyAsync(e => e.CompanyId == companyId && !string.IsNullOrEmpty(e.Error));

            if (hasEmployeeErrors)
            {
                return BadRequest(new { 
                    message = "Cannot upload payroll. Please fix all employee master errors first.",
                    canUpload = false 
                });
            }

            return Ok(new { canUpload = true });
        }

        // Upload payroll file and create/update PayrollEntry records
        [HttpPost("upload")]
        public async Task<IActionResult> UploadPayroll([FromForm] PayrollUploadRequest request)
        {
            try
            {
                // Check if payroll upload is allowed
                var canUpload = await CanUploadPayroll(request.CompanyId);
                if (canUpload is BadRequestObjectResult)
                    return canUpload;

                var payrollMonth = await _context.PayrollMonth
                    .FirstOrDefaultAsync(pm => pm.Id == request.PayrollMonthId);
                
                if (payrollMonth == null)
                    return BadRequest("Invalid payroll month.");

                var totalDaysInMonth = payrollMonth.TotalDays; // Use the TotalDays field directly
                var uploadedEmployees = new List<PayrollEntry>();
                var errors = new List<string>();

                using (var stream = new MemoryStream())
                {
                    await request.File.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheets.First();
                        
                        // Simple column mapping (similar to employee upload)
                        Dictionary<string, int> colMap = new();
                        int headerRowNum = 1;
                        
                        void ReadHeaderRow(int rowNum)
                        {
                            colMap.Clear();
                            var headerRow = worksheet.Row(rowNum);
                            for (int col = 1; col <= worksheet.LastColumnUsed().ColumnNumber(); col++)
                            {
                                try
                                {
                                    var colName = headerRow.Cell(col).GetString()?.Trim()?.ToLower() ?? "";
                                    if (!string.IsNullOrEmpty(colName) && !colMap.ContainsKey(colName))
                                        colMap[colName] = col;
                                }
                                catch
                                {
                                    // Skip columns that can't be read
                                }
                            }
                        }
                        
                        ReadHeaderRow(1);
                        
                        // Helper to find column index by exact name matching
                        int FindCol(string exactName) 
                        {
                            return colMap.TryGetValue(exactName.ToLower(), out var colIndex) ? colIndex : 0;
                        }
                        
                        // Map columns with exact name matching
                        int colPF = FindCol("pf");
                        int colESI = FindCol("esi");
                        int colName = FindCol("name");
                        int colPresentDays = FindCol("working_days");
                        int colBasicSalary = FindCol("basic");
                        int colGrossSalary = FindCol("gross_salary");
                        
                        var missingCols = new List<string>();
                        if (colPF == 0 && colESI == 0) missingCols.Add("pf or esi");
                        if (colName == 0) missingCols.Add("name");
                        if (colPresentDays == 0) missingCols.Add("working_days");
                        if (colBasicSalary == 0) missingCols.Add("basic");
                        if (colGrossSalary == 0) missingCols.Add("gross_salary");
                        
                        // If not all required columns found, try row 2 as header
                        if (missingCols.Count > 0)
                        {
                            ReadHeaderRow(2);
                            missingCols.Clear();
                            
                            // Re-map columns with exact names
                            colPF = FindCol("pf");
                            colESI = FindCol("esi");
                            colName = FindCol("name");
                            colPresentDays = FindCol("working_days");
                            colBasicSalary = FindCol("basic");
                            colGrossSalary = FindCol("gross_salary");
                            
                            if (colPF == 0 && colESI == 0) missingCols.Add("pf or esi");
                            if (colName == 0) missingCols.Add("name");
                            if (colPresentDays == 0) missingCols.Add("working_days");
                            if (colBasicSalary == 0) missingCols.Add("basic");
                            if (colGrossSalary == 0) missingCols.Add("gross_salary");
                            
                            headerRowNum = 2;
                        }
                        
                        if (missingCols.Count > 0)
                            return BadRequest($"Missing required column(s): {string.Join(", ", missingCols)}");

                        int rowNum = headerRowNum + 1;
                        Console.WriteLine($"Starting to process Excel rows from row {rowNum}");
                        
                        while (true)
                        {
                            // Get cell values safely
                            var pfNumber = colPF > 0 ? worksheet.Cell(rowNum, colPF).GetString()?.Trim() ?? "" : "";
                            var esiNumber = colESI > 0 ? worksheet.Cell(rowNum, colESI).GetString()?.Trim() ?? "" : "";
                            var name = colName > 0 ? worksheet.Cell(rowNum, colName).GetString()?.Trim() ?? "" : "";
                            var presentDaysStr = colPresentDays > 0 ? worksheet.Cell(rowNum, colPresentDays).GetString()?.Trim() ?? "" : "";
                            var basicSalaryStr = colBasicSalary > 0 ? worksheet.Cell(rowNum, colBasicSalary).GetString()?.Trim() ?? "" : "";
                            var grossSalaryStr = colGrossSalary > 0 ? worksheet.Cell(rowNum, colGrossSalary).GetString()?.Trim() ?? "" : "";
                            
                            // Break if no employee identifier found
                            if (string.IsNullOrWhiteSpace(pfNumber) && string.IsNullOrWhiteSpace(esiNumber))
                            {
                                Console.WriteLine($"No employee identifier found at row {rowNum}, ending processing");
                                break;
                            }
                                
                            Console.WriteLine($"Processing row {rowNum}: PF={pfNumber}, ESI={esiNumber}, Name={name}, Present Days={presentDaysStr}");
                            
                            // Normalize "NIL" values to empty
                            if (string.Equals(pfNumber, "nil", StringComparison.OrdinalIgnoreCase)) pfNumber = "";
                            if (string.Equals(esiNumber, "nil", StringComparison.OrdinalIgnoreCase)) esiNumber = "";

                            // Parse numeric values
                            if (!decimal.TryParse(presentDaysStr, out decimal presentDays))
                            {
                                errors.Add($"Row {rowNum - headerRowNum}: Invalid present days '{presentDaysStr}' for employee '{name}'");
                                rowNum++;
                                continue;
                            }

                            if (!decimal.TryParse(basicSalaryStr, out decimal basicSalary))
                            {
                                errors.Add($"Row {rowNum - headerRowNum}: Invalid basic salary '{basicSalaryStr}' for employee '{name}'");
                                rowNum++;
                                continue;
                            }

                            if (!decimal.TryParse(grossSalaryStr, out decimal grossSalary))
                            {
                                errors.Add($"Row {rowNum - headerRowNum}: Invalid gross salary '{grossSalaryStr}' for employee '{name}'");
                                rowNum++;
                                continue;
                            }

                            // Find employee in master table
                            var normalizedPfNumber = !string.IsNullOrWhiteSpace(pfNumber) ? pfNumber.Trim() : null;
                            var normalizedEsiNumber = !string.IsNullOrWhiteSpace(esiNumber) ? esiNumber.Trim() : null;
                            
                            if (normalizedPfNumber == null && normalizedEsiNumber == null)
                            {
                                errors.Add($"Row {rowNum - headerRowNum}: No valid PF or ESI number for employee '{name}'");
                                rowNum++;
                                continue;
                            }
                            
                            var allCompanyEmployees = await _context.Employee
                                .Where(e => e.CompanyId == request.CompanyId)
                                .ToListAsync();
                                
                            var employee = allCompanyEmployees
                                .FirstOrDefault(e => 
                                    (normalizedPfNumber != null && !string.IsNullOrWhiteSpace(e.PFNumber) && 
                                     e.PFNumber.Trim().Equals(normalizedPfNumber, StringComparison.OrdinalIgnoreCase)) ||
                                    (normalizedEsiNumber != null && !string.IsNullOrWhiteSpace(e.ESINumber) && 
                                     e.ESINumber.Trim().Equals(normalizedEsiNumber, StringComparison.OrdinalIgnoreCase)));

                            if (employee == null)
                            {
                                var identifier = normalizedPfNumber != null ? $"PF: '{normalizedPfNumber}'" : $"ESI: '{normalizedEsiNumber}'";
                                errors.Add($"Row {rowNum - headerRowNum}: Employee with {identifier} not found in master table");
                                rowNum++;
                                continue;
                            }

                            // Calculate NCP (Non-Contributing Period) as decimal
                            var ncp = Math.Max(0, (decimal)totalDaysInMonth - presentDays);
                            var reason = 0; // Default reason code (0 = no specific reason)

                            var payrollEntry = new PayrollEntry
                            {
                                EmployeeId = employee.Id,
                                CompanyId = request.CompanyId,
                                PayrollMonthId = request.PayrollMonthId,
                                WorkingDays = presentDays,
                                BasicDA = basicSalary,
                                GrossSalary = grossSalary,
                                NCP = ncp,  // Now decimal
                                Reason = reason  // Now int
                            };

                            uploadedEmployees.Add(payrollEntry);
                            rowNum++;
                        }
                    }
                }

                // Add missing employees (who don't have leaving date) with 0 working days
                await AddMissingEmployees(request.CompanyId, request.PayrollMonthId, uploadedEmployees, totalDaysInMonth);

                // Check if there are any errors before proceeding with save
                if (errors.Any())
                {
                    return BadRequest(new { 
                        message = "Upload failed with errors. Please fix the data and re-upload.",
                        errors = errors,
                        uploadedCount = 0,
                        totalErrors = errors.Count
                    });
                }

                Console.WriteLine($"About to save {uploadedEmployees.Count} payroll entries to database");
                
                // Clear existing payroll entries for this company and month ONLY if upload is successful
                var existingEntries = await _context.PayrollEntry
                    .Where(pe => pe.CompanyId == request.CompanyId && pe.PayrollMonthId == request.PayrollMonthId)
                    .ToListAsync();
                
                if (existingEntries.Any())
                {
                    _context.PayrollEntry.RemoveRange(existingEntries);
                }

                // Save all payroll entries
                _context.PayrollEntry.AddRange(uploadedEmployees);
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"Successfully saved {uploadedEmployees.Count} payroll entries to database");

                return Ok(new { 
                    message = "Payroll uploaded successfully",
                    uploadedCount = uploadedEmployees.Count,
                    totalEmployees = uploadedEmployees.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error uploading file: {ex.Message}");
            }
        }

        // Get payroll data for a company and month
        [HttpGet("payroll/{companyId}/{payrollMonthId}")]
        public async Task<IActionResult> GetPayrollData(int companyId, int payrollMonthId,
            [FromQuery] string? searchName = null, 
            [FromQuery] string? searchPF = null, 
            [FromQuery] string? searchESI = null)
        {
            var query = _context.PayrollEntry
                .Include(pe => pe.Employee)
                .Where(pe => pe.CompanyId == companyId && 
                           pe.PayrollMonthId == payrollMonthId &&
                           pe.Employee.IsActive == true); // Only show active employees
            
            // Apply search filters if provided
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(pe => pe.Employee.Name.ToLower().Contains(searchName.ToLower()));
            }
            
            if (!string.IsNullOrWhiteSpace(searchPF))
            {
                query = query.Where(pe => pe.Employee.PFNumber != null && 
                                        pe.Employee.PFNumber.ToLower().Contains(searchPF.ToLower()));
            }
            
            if (!string.IsNullOrWhiteSpace(searchESI))
            {
                query = query.Where(pe => pe.Employee.ESINumber != null && 
                                        pe.Employee.ESINumber.ToLower().Contains(searchESI.ToLower()));
            }
            
            var payrollData = await query
                .Select(pe => new
                {
                    pe.Id,
                    pe.EmployeeId,
                    pe.WorkingDays,
                    pe.BasicDA,
                    pe.GrossSalary,
                    pe.NCP,
                    pe.Reason,
                    Employee = new
                    {
                        pe.Employee.Id,
                        pe.Employee.Name,
                        pe.Employee.PFNumber,
                        pe.Employee.ESINumber,
                        pe.Employee.LeavingDate,
                        pe.Employee.IsActive,
                        HasLeavingDate = pe.Employee.LeavingDate != null
                    }
                })
                .OrderBy(pe => pe.WorkingDays == 0 ? 0 : 1) // Zero working days first
                .ThenBy(pe => pe.Employee.Name)
                .ToListAsync();

            return Ok(payrollData);
        }

        // Add new payroll entry
        [HttpPost("add-entry")]
        public async Task<IActionResult> AddPayrollEntry([FromBody] CreatePayrollEntryRequest request)
        {
            var employee = await _context.Employee.FindAsync(request.EmployeeId);
            if (employee == null)
                return BadRequest("Employee not found.");

            var payrollMonth = await _context.PayrollMonth.FindAsync(request.PayrollMonthId);
            if (payrollMonth == null)
                return BadRequest("Payroll month not found.");

            var totalDaysInMonth = payrollMonth.TotalDays; // Use TotalDays field directly
            var ncpDecimal = Math.Max(0, (decimal)totalDaysInMonth - request.WorkingDays);

            var payrollEntry = new PayrollEntry
            {
                EmployeeId = request.EmployeeId,
                CompanyId = employee.CompanyId,
                PayrollMonthId = request.PayrollMonthId,
                WorkingDays = request.WorkingDays,
                BasicDA = request.BasicDA,
                GrossSalary = request.GrossSalary,
                NCP = ncpDecimal,  // Now decimal
                Reason = request.Reason  // Now int
            };

            _context.PayrollEntry.Add(payrollEntry);
            await _context.SaveChangesAsync();

            return Ok(payrollEntry);
        }

        // Update payroll entry
        [HttpPut("update-entry/{id}")]
        public async Task<IActionResult> UpdatePayrollEntry(int id, [FromBody] UpdatePayrollEntryRequest request)
        {
            var entry = await _context.PayrollEntry.FindAsync(id);
            if (entry == null)
                return NotFound("Payroll entry not found.");

            var payrollMonth = await _context.PayrollMonth.FindAsync(entry.PayrollMonthId);
            var totalDaysInMonth = payrollMonth.TotalDays; // Use TotalDays field directly
            var ncpDecimal = Math.Max(0, (decimal)totalDaysInMonth - request.WorkingDays);

            entry.WorkingDays = request.WorkingDays;
            entry.BasicDA = request.BasicDA;
            entry.GrossSalary = request.GrossSalary;
            entry.NCP = ncpDecimal;  // Now decimal
            entry.Reason = request.Reason;  // Now int

            await _context.SaveChangesAsync();
            return Ok(entry);
        }

        // Delete payroll entry
        [HttpDelete("delete-entry/{id}")]
        public async Task<IActionResult> DeletePayrollEntry(int id)
        {
            var entry = await _context.PayrollEntry.FindAsync(id);
            if (entry == null)
                return NotFound("Payroll entry not found.");

            _context.PayrollEntry.Remove(entry);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Payroll entry deleted successfully" });
        }

        // Check if reports can be downloaded
        [HttpGet("can-download/{companyId}/{payrollMonthId}")]
        public async Task<IActionResult> CanDownloadReports(int companyId, int payrollMonthId)
        {
            // Check employee master has no errors
            var hasEmployeeErrors = await _context.Employee
                .AnyAsync(e => e.CompanyId == companyId && !string.IsNullOrEmpty(e.Error));

            if (hasEmployeeErrors)
            {
                return BadRequest(new { 
                    message = "Cannot download reports. Employee master table has errors.",
                    canDownload = false 
                });
            }

            // Check payroll validation rules (updated to exclude leaving employees from PF requirements)
            var invalidEntries = await _context.PayrollEntry
                .Include(pe => pe.Employee)
                .Where(pe => pe.CompanyId == companyId && pe.PayrollMonthId == payrollMonthId)
                .Where(pe => pe.WorkingDays == 0 && 
                    pe.Employee.LeavingDate == null && // Only check validation for non-leaving employees
                    pe.Reason == 0) // Non-leaving employees with 0 working days must have proper reason
                .AnyAsync();

            if (invalidEntries)
            {
                return BadRequest(new { 
                    message = "Cannot download reports. Active employees with 0 working days must have proper reason codes.",
                    canDownload = false 
                });
            }

            return Ok(new { canDownload = true });
        }

        // Download PF Report (ECR Challan)
        [HttpGet("download-pf/{companyId}/{payrollMonthId}")]
        public async Task<IActionResult> DownloadPFReport(int companyId, int payrollMonthId)
        {
            // Add CORS headers for file downloads
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Methods"] = "GET";
            Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Accept";
            
            var canDownload = await CanDownloadReports(companyId, payrollMonthId);
            if (canDownload is BadRequestObjectResult badRequest)
            {
                // Ensure error responses are JSON
                Response.ContentType = "application/json";
                return badRequest;
            }

            try
            {
                Console.WriteLine($"Starting PF download for company {companyId}, payroll month {payrollMonthId}");
                
                // Get payroll data for ECR generation (excluding leaving employees)
                var payrollData = await _context.PayrollEntry
                    .Include(pe => pe.Employee)
                    .Where(pe => pe.CompanyId == companyId && 
                                pe.PayrollMonthId == payrollMonthId &&
                                pe.Employee.IsActive == true &&
                                !string.IsNullOrWhiteSpace(pe.Employee.PFNumber) &&
                                pe.Employee.LeavingDate == null && // Exclude employees with leaving date
                                pe.WorkingDays > 0) // Only include employees with working days > 0
                    .OrderBy(pe => pe.Employee.Name)
                    .ToListAsync();

                Console.WriteLine($"Found {payrollData.Count} eligible employees for PF report");

                if (!payrollData.Any())
                {
                    Response.ContentType = "application/json";
                    return BadRequest(new { message = "No eligible employees found for PF report generation." });
                }

                // Get company and payroll month details separately
                var company = await _context.Company.FindAsync(companyId);
                var payrollMonth = await _context.PayrollMonth.FindAsync(payrollMonthId);
                
                if (payrollMonth == null)
                {
                    Response.ContentType = "application/json";
                    return BadRequest(new { message = "Payroll month not found." });
                }

                // Generate ECR Challan in TXT format
                var ecrTextContent = new StringBuilder();
                
                // Add header line for TXT format
                ecrTextContent.AppendLine("UAN#~#Employee Name#~#Gross Wages#~#EPF Wages#~#EPS Wages#~#EDLI Wages#~#EE Share#~#EPS Contribution#~#ER Share#~#NCP Days#~#Reason#~#Refund");
                
                decimal totalEEShare = 0;
                decimal totalEPSContribution = 0;
                decimal totalERShare = 0;

                foreach (var entry in payrollData)
                {
                    var ecrData = CalculateECRValues(entry);
                    
                    // Since leaving employees are excluded, reason code is always 0 for active employees
                    var reasonCode = 0;
                    
                    // Generate ECR text line
                    var ecrLine = $"{entry.Employee.PFNumber}#~#{entry.Employee.Name}#~#{entry.GrossSalary}#~#" +
                                 $"{ecrData.EPFWages}#~#{ecrData.EPSWages}#~#{ecrData.EDLIWages}#~#" +
                                 $"{ecrData.EEShare}#~#{ecrData.EPSContribution}#~#{ecrData.ERShare}#~#" +
                                 $"{Math.Round(entry.NCP, 0)}#~#{reasonCode}#~#{ecrData.Refund}";
                    
                    ecrTextContent.AppendLine(ecrLine);

                    totalEEShare += ecrData.EEShare;
                    totalEPSContribution += ecrData.EPSContribution;
                    totalERShare += ecrData.ERShare;
                }

                // Add totals line
                var totalLine = $"TOTAL#~##~##~##~##~##~#{totalEEShare}#~#{totalEPSContribution}#~#{totalERShare}#~##~##~#";
                ecrTextContent.AppendLine(totalLine);
                
                // Convert to bytes
                var fileBytes = Encoding.UTF8.GetBytes(ecrTextContent.ToString());
                var fileName = $"ECR_Challan_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                
                Console.WriteLine($"Generated ECR TXT file: {fileName}, Size: {fileBytes.Length} bytes");
                
                // Clear any previous content type and set proper TXT headers
                Response.Clear();
                Response.ContentType = "text/plain";
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";
                
                // Return the TXT file
                return File(fileBytes, "text/plain", fileName);

                /* COMMENTED OUT - Excel format code
                // Generate ECR Challan Excel
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add($"ECR_{payrollMonth.Month}");
                    
                    // Set up headers
                    var headers = new[]
                    {
                        "UAN", "Employee Name", "Gross Wages", "EPF Wages", "EPS Wages", "EDLI Wages",
                        "EE Share", "EPS Contribution", "ER Share", "NCP Days", "Reason", "Refund"
                    };

                    // Add headers
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = headers[i];
                        worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                        worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    int row = 2;
                    decimal totalEEShare = 0;
                    decimal totalEPSContribution = 0;
                    decimal totalERShare = 0;

                    foreach (var entry in payrollData)
                    {
                        var ecrData = CalculateECRValues(entry);
                        
                        // Calculate proper reason code for ECR
                        var reasonCode = (entry.WorkingDays == 0 || entry.Employee.LeavingDate != null) ? entry.Reason : 0;
                        
                        worksheet.Cell(row, 1).Value = entry.Employee.PFNumber; // UAN
                        worksheet.Cell(row, 2).Value = entry.Employee.Name;
                        worksheet.Cell(row, 3).Value = (double)entry.GrossSalary; // Gross Wages
                        worksheet.Cell(row, 4).Value = (double)ecrData.EPFWages; // EPF Wages (Basic+DA)
                        worksheet.Cell(row, 5).Value = (double)ecrData.EPSWages; // EPS Wages (Basic+DA capped at 15K)
                        worksheet.Cell(row, 6).Value = (double)ecrData.EDLIWages; // EDLI Wages (Basic+DA capped at 15K)
                        worksheet.Cell(row, 7).Value = (double)ecrData.EEShare; // Employee Share (12% of Basic+DA)
                        worksheet.Cell(row, 8).Value = (double)ecrData.EPSContribution; // EPS Contribution (8.33% of capped Basic+DA)
                        worksheet.Cell(row, 9).Value = (double)ecrData.ERShare; // Employer Share (12% - 8.33% of capped amount)
                        worksheet.Cell(row, 10).Value = (double)entry.NCP; // NCP Days
                        worksheet.Cell(row, 11).Value = reasonCode; // Reason (mandatory for leaving employees)
                        worksheet.Cell(row, 12).Value = (double)ecrData.Refund; // Refund (usually 0)

                        totalEEShare += ecrData.EEShare;
                        totalEPSContribution += ecrData.EPSContribution;
                        totalERShare += ecrData.ERShare;

                        row++;
                    }

                    // Add totals row
                    worksheet.Cell(row + 1, 1).Value = "TOTAL";
                    worksheet.Cell(row + 1, 1).Style.Font.Bold = true;
                    worksheet.Cell(row + 1, 7).Value = (double)totalEEShare;
                    worksheet.Cell(row + 1, 7).Style.Font.Bold = true;
                    worksheet.Cell(row + 1, 8).Value = (double)totalEPSContribution;
                    worksheet.Cell(row + 1, 8).Style.Font.Bold = true;
                    worksheet.Cell(row + 1, 9).Value = (double)totalERShare;
                    worksheet.Cell(row + 1, 9).Style.Font.Bold = true;

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Add ECR text format sheet
                    var ecrTextSheet = workbook.Worksheets.Add("ECR_Text_Format");
                    ecrTextSheet.Cell(1, 1).Value = "ECR Text Format (Copy this for EPFO upload)";
                    ecrTextSheet.Cell(1, 1).Style.Font.Bold = true;
                    
                    int textRow = 3;
                    foreach (var entry in payrollData)
                    {
                        var ecrData = CalculateECRValues(entry);
                        var ecrLine = GenerateECRTextLine(entry, ecrData);
                        ecrTextSheet.Cell(textRow, 1).Value = ecrLine;
                        textRow++;
                    }

                    // Save to memory stream
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        stream.Position = 0; // Reset stream position
                        
                        var fileName = $"ECR_Challan_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                        var fileBytes = stream.ToArray();
                        
                        Console.WriteLine($"Generated ECR file: {fileName}, Size: {fileBytes.Length} bytes");
                        
                        // Clear any previous content type and set proper Excel headers
                        Response.Clear();
                        Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                        Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
                        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                        Response.Headers["Pragma"] = "no-cache";
                        Response.Headers["Expires"] = "0";
                        
                        // Return the file
                        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
                */
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating ECR Challan: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Response.ContentType = "application/json";
                return StatusCode(500, new { message = $"Error generating ECR Challan: {ex.Message}" });
            }
        }

        // Download ESI Report using NPOI for proper .xls format handling
        [HttpGet("download-esi/{companyId}/{payrollMonthId}")]
        public async Task<IActionResult> DownloadESIReport(int companyId, int payrollMonthId)
        {
            // Add CORS headers for file downloads
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Methods"] = "GET";
            Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Accept";
            
            var canDownload = await CanDownloadReports(companyId, payrollMonthId);
            if (canDownload is BadRequestObjectResult badRequest)
            {
                // Ensure error responses are JSON
                Response.ContentType = "application/json";
                return badRequest;
            }

            try
            {
                Console.WriteLine($"Starting ESI download for company {companyId}, payroll month {payrollMonthId}");
                
                // Get payroll data for ESI generation
                var payrollData = await _context.PayrollEntry
                    .Include(pe => pe.Employee)
                    .Where(pe => pe.CompanyId == companyId && 
                                pe.PayrollMonthId == payrollMonthId &&
                                pe.Employee.IsActive == true &&
                                !string.IsNullOrWhiteSpace(pe.Employee.ESINumber) &&
                                (pe.WorkingDays > 0 || // Include employees with working days > 0
                                 (pe.WorkingDays == 0 && pe.Employee.LeavingDate != null && pe.Reason != 0))) // Include employees with 0 working days if they have leaving date and reason
                    .OrderBy(pe => pe.Employee.Name)
                    .ToListAsync();

                Console.WriteLine($"Found {payrollData.Count} eligible employees for ESI report");

                if (!payrollData.Any())
                {
                    Response.ContentType = "application/json";
                    return BadRequest(new { message = "No eligible employees found for ESI report generation." });
                }

                // Get company and payroll month details
                var company = await _context.Company.FindAsync(companyId);
                var payrollMonth = await _context.PayrollMonth.FindAsync(payrollMonthId);
                
                if (payrollMonth == null)
                {
                    Response.ContentType = "application/json";
                    return BadRequest(new { message = "Payroll month not found." });
                }

                // Generate ESI report using NPOI
                var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "ESI_Template.xls");
                
                if (!System.IO.File.Exists(templatePath))
                {
                    Response.ContentType = "application/json";
                    return BadRequest(new { message = "ESI template file not found." });
                }

                try
                {
                    var finalFileName = $"ESI_Report_{payrollMonth.Month}_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
                    
                    Console.WriteLine("Generating ESI report using NPOI for proper .xls format");
                    
                    // Use NPOI to handle .xls file format properly
                    HSSFWorkbook workbook;
                    using (var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read))
                    {
                        workbook = new HSSFWorkbook(templateStream);
                    }
                    
                    var worksheet = workbook.GetSheetAt(0);
                    
                    // Fill in the ESI data while preserving template formatting
                    await FillESIDataWithNPOI(worksheet, payrollData, company, payrollMonth);
                    
                    // Generate file bytes
                    using (var outputStream = new MemoryStream())
                    {
                        workbook.Write(outputStream);
                        var fileBytes = outputStream.ToArray();
                        
                        Console.WriteLine($"ESI file generated with NPOI: {finalFileName}, Size: {fileBytes.Length} bytes");
                        
                        // Set proper headers for .xls file download
                        Response.Clear();
                        Response.ContentType = "application/vnd.ms-excel";
                        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{finalFileName}\"";
                        Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
                        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                        Response.Headers["Pragma"] = "no-cache";
                        Response.Headers["Expires"] = "0";
                        
                        Console.WriteLine($"Sending ESI file with data for download: {finalFileName}");
                        
                        // Close the workbook to free resources
                        workbook.Close();
                        
                        // Return the populated file for download
                        return File(fileBytes, "application/vnd.ms-excel", finalFileName);
                    }
                }
                catch (Exception templateEx)
                {
                    Console.WriteLine($"Template processing failed: {templateEx.Message}");
                    Console.WriteLine($"Template error stack trace: {templateEx.StackTrace}");
                    
                    // If template can't be opened, create a new ESI report with same structure
                    Console.WriteLine("Creating ESI report from scratch due to template error");
                    return await GenerateESIReportWithoutTemplate(payrollData, company, payrollMonth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating ESI Report: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Response.ContentType = "application/json";
                return StatusCode(500, new { message = $"Error generating ESI Report: {ex.Message}" });
            }
        }

        private async Task AddMissingEmployees(int companyId, int payrollMonthId, List<PayrollEntry> uploadedEmployees, int totalDaysInMonth)
        {
            var uploadedEmployeeIds = uploadedEmployees.Select(ue => ue.EmployeeId).ToList();
            
            var missingEmployees = await _context.Employee
                .Where(e => e.CompanyId == companyId && 
                           e.IsActive == true && // Only active employees
                           e.LeavingDate == null && // Exclude employees with leaving dates
                           !uploadedEmployeeIds.Contains(e.Id))
                .ToListAsync();

            foreach (var employee in missingEmployees)
            {
                var missingEntry = new PayrollEntry
                {
                    EmployeeId = employee.Id,
                    CompanyId = companyId,
                    PayrollMonthId = payrollMonthId,
                    WorkingDays = 0m, // Explicitly use decimal
                    BasicDA = 0m,     // Explicitly use decimal
                    GrossSalary = 0m, // Explicitly use decimal
                    NCP = (decimal)totalDaysInMonth,  // Cast to decimal
                    Reason = 0  // Default reason code
                };

                uploadedEmployees.Add(missingEntry);
            }
        }

        private int GetTotalDaysInMonth(string monthYear)
        {
            if (DateTime.TryParse($"1 {monthYear}", out var date))
            {
                return DateTime.DaysInMonth(date.Year, date.Month);
            }
            return 30; // Default fallback
        }

        private ECRCalculationResult CalculateECRValues(PayrollEntry entry)
        {
            var basicDA = entry.BasicDA;
            var basic15K = Math.Min(basicDA, 15000); // Cap at 15,000 for EPS/EDLI calculation
            
            // Round to integers for PF calculations (no decimals)
            var eeShare = Math.Round(basicDA * 12 / 100, 0); // 12% of actual Basic+DA, rounded to integer
            var epsContribution = Math.Round(basic15K * 8.33m / 100, 0); // 8.33% of capped Basic+DA, rounded to integer
            var erShare = Math.Round(basicDA * 12 / 100, 0) - epsContribution; // 12% minus EPS contribution, both rounded
            
            return new ECRCalculationResult
            {
                EPFWages = basicDA,
                EPSWages = basic15K,
                EDLIWages = basic15K,
                EEShare = eeShare,
                EPSContribution = epsContribution,
                ERShare = erShare,
                Refund = 0 // Usually 0
            };
        }

        private string GenerateECRTextLine(PayrollEntry entry, ECRCalculationResult ecrData)
        {
            // Calculate proper reason code for ECR text format
            var reasonCode = (entry.WorkingDays == 0 || entry.Employee.LeavingDate != null) ? entry.Reason : 0;
            
            // Format: UAN#~#Name#~#Gross#~#EPF#~#EPS#~#EDLI#~#EEShare#~#EPSContrib#~#ERShare#~#NCP#~#Reason#~#Refund
            return $"{entry.Employee.PFNumber}#~#{entry.Employee.Name}#~#{entry.GrossSalary}#~#" +
                   $"{ecrData.EPFWages}#~#{ecrData.EPSWages}#~#{ecrData.EDLIWages}#~#" +
                   $"{ecrData.EEShare}#~#{ecrData.EPSContribution}#~#{ecrData.ERShare}#~#" +
                   $"{Math.Round(entry.NCP, 0)}#~#{reasonCode}#~#{ecrData.Refund}";
        }

        // Enhanced method to fill ESI data with formatting preservation using NPOI
        private Task FillESIDataWithFormatting(ISheet worksheet, List<PayrollEntry> payrollData, Company company, PayrollMonth payrollMonth, int startRow)
        {
            try
            {
                Console.WriteLine($"Filling ESI data for {payrollData.Count} employees starting from row {startRow}");
                
                int currentRow = startRow;
                
                foreach (var entry in payrollData)
                {
                    // Create row if it doesn't exist
                    var row = worksheet.GetRow(currentRow) ?? worksheet.CreateRow(currentRow);
                    
                    // Calculate ESI wages using available properties
                    // For ESI, we typically use gross salary but cap it at ESI ceiling (21,000 for 2024)
                    decimal esiWages = Math.Min(entry.GrossSalary, 21000); // ESI limit
                    
                    // Fill data columns based on the actual ESI template structure:
                    // Column 1: IP Number (10 Digits)
                    var cell1 = row.GetCell(0) ?? row.CreateCell(0);
                    cell1.SetCellValue(entry.Employee.ESINumber ?? "");
                    
                    // Column 2: IP Name (Only alphabets and space)
                    var cell2 = row.GetCell(1) ?? row.CreateCell(1);
                    cell2.SetCellValue(entry.Employee.Name ?? "");
                    
                    // Column 3: No of Days for which wages paid/payable during the month
                    var cell3 = row.GetCell(2) ?? row.CreateCell(2);
                    cell3.SetCellValue((double)entry.WorkingDays);
                    
                    // Column 4: Total Monthly Wages
                    var cell4 = row.GetCell(3) ?? row.CreateCell(3);
                    cell4.SetCellValue((double)esiWages);
                    
                    // Column 5: Reason Code for Zero workings days (numeric only; provide 0 for all other reasons)
                    var cell5 = row.GetCell(4) ?? row.CreateCell(4);
                    var reasonCode = (entry.WorkingDays == 0 || entry.Employee.LeavingDate != null) ? entry.Reason : 0;
                    cell5.SetCellValue(reasonCode);
                    
                    // Column 6: Last Working Day (Format DD/MM/YYYY or DD-MM-YYYY)
                    var cell6 = row.GetCell(5) ?? row.CreateCell(5);
                    var lastWorkingDay = entry.Employee.LeavingDate?.ToString("dd/MM/yyyy") ?? "";
                    cell6.SetCellValue(lastWorkingDay);
                    
                    Console.WriteLine($"Row {currentRow}: {entry.Employee.Name}, ESI: {entry.Employee.ESINumber}, Wages: {esiWages}, Days: {entry.WorkingDays}");
                    
                    currentRow++;
                }
                
                Console.WriteLine($"ESI data filled for {payrollData.Count} employees");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error filling ESI data: {ex.Message}");
                throw;
            }
        }

        // Fallback method to generate ESI report without template using NPOI
        private async Task<IActionResult> GenerateESIReportWithoutTemplate(List<PayrollEntry> payrollData, Company company, PayrollMonth payrollMonth)
        {
            try
            {
                Console.WriteLine("Generating ESI report without template using NPOI");
                
                // Create a new NPOI workbook for .xls format
                var workbook = new HSSFWorkbook();
                var worksheet = workbook.CreateSheet("ESI_Report");
                
                // Setup ESI headers using NPOI
                SetupESIHeadersWithNPOI(worksheet);
                
                // Fill data starting from row 1 (row 0 has headers)
                await FillESIDataWithFormatting(worksheet, payrollData, company, payrollMonth, 1);
                
                using (var stream = new MemoryStream())
                {
                    workbook.Write(stream);
                    var fileBytes = stream.ToArray();
                    
                    var fileName = $"ESI_Report_{payrollMonth.Month}_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
                    
                    Console.WriteLine($"Generated fallback ESI file using NPOI: {fileName}");
                    
                    Response.Clear();
                    Response.ContentType = "application/vnd.ms-excel";
                    Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                    Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
                    
                    // Close the workbook to free resources
                    workbook.Close();
                    
                    return File(fileBytes, "application/vnd.ms-excel", fileName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in fallback ESI generation: {ex.Message}");
                Response.ContentType = "application/json";
                return StatusCode(500, new { message = $"Error generating ESI Report: {ex.Message}" });
            }
        }

        // Fill ESI data using NPOI for proper .xls format handling
        private Task FillESIDataWithNPOI(ISheet worksheet, List<PayrollEntry> payrollData, Company company, PayrollMonth payrollMonth)
        {
            try
            {
                Console.WriteLine($"Filling ESI data using NPOI for {payrollData.Count} employees");
                
                // Find the data starting row by looking for headers
                int dataStartRow = FindDataStartRowNPOI(worksheet);
                Console.WriteLine($"ESI data will start from row: {dataStartRow}");
                
                int currentRow = dataStartRow;
                
                foreach (var entry in payrollData)
                {
                    // Create row if it doesn't exist
                    var row = worksheet.GetRow(currentRow) ?? worksheet.CreateRow(currentRow);
                    
                    // Calculate ESI wages using available properties
                    // For ESI, we typically use gross salary but cap it at ESI ceiling (21,000 for 2024)
                    decimal esiWages = Math.Min(entry.GrossSalary, 21000); // ESI limit
                    
                    // Fill data columns based on the actual ESI template structure:
                    // Column 1: IP Number (10 Digits)
                    var cell1 = row.GetCell(0) ?? row.CreateCell(0);
                    cell1.SetCellValue(entry.Employee.ESINumber ?? "");
                    
                    // Column 2: IP Name (Only alphabets and space)
                    var cell2 = row.GetCell(1) ?? row.CreateCell(1);
                    cell2.SetCellValue(entry.Employee.Name ?? "");
                    
                    // Column 3: No of Days for which wages paid/payable during the month
                    var cell3 = row.GetCell(2) ?? row.CreateCell(2);
                    cell3.SetCellValue((double)entry.WorkingDays);
                    
                    // Column 4: Total Monthly Wages
                    var cell4 = row.GetCell(3) ?? row.CreateCell(3);
                    cell4.SetCellValue((double)esiWages);
                    
                    // Column 5: Reason Code for Zero workings days (numeric only; provide 0 for all other reasons)
                    var cell5 = row.GetCell(4) ?? row.CreateCell(4);
                    // Use reason code for employees with 0 working days or those who have left
                    var reasonCode = (entry.WorkingDays == 0 || entry.Employee.LeavingDate != null) ? entry.Reason : 0;
                    cell5.SetCellValue(reasonCode);
                    
                    // Column 6: Last Working Day (Format DD/MM/YYYY or DD-MM-YYYY)
                    var cell6 = row.GetCell(5) ?? row.CreateCell(5);
                    // Show leaving date if employee has left, otherwise empty
                    var lastWorkingDay = entry.Employee.LeavingDate?.ToString("dd/MM/yyyy") ?? "";
                    cell6.SetCellValue(lastWorkingDay);
                    
                    Console.WriteLine($"Row {currentRow}: {entry.Employee.Name}, ESI: {entry.Employee.ESINumber}, Wages: {esiWages}, Days: {entry.WorkingDays}");
                    
                    currentRow++;
                }
                
                Console.WriteLine($"ESI data filled using NPOI for {payrollData.Count} employees");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error filling ESI data with NPOI: {ex.Message}");
                throw;
            }
        }

        // Helper method to setup ESI headers using NPOI
        private void SetupESIHeadersWithNPOI(ISheet worksheet)
        {
            // Create header row
            var headerRow = worksheet.CreateRow(0);
            
            // Set headers exactly as they appear in the original template
            var cell1 = headerRow.CreateCell(0);
            cell1.SetCellValue("IP Number \n(10 Digits)");
            
            var cell2 = headerRow.CreateCell(1);
            cell2.SetCellValue("IP Name\n( Only alphabets and space )");
            
            var cell3 = headerRow.CreateCell(2);
            cell3.SetCellValue("No of Days for which wages paid/payable during the month");
            
            var cell4 = headerRow.CreateCell(3);
            cell4.SetCellValue("Total Monthly Wages");
            
            var cell5 = headerRow.CreateCell(4);
            cell5.SetCellValue(" Reason Code for Zero \n workings days(numeric only; provide 0 for all other reasons- Click on the link for reference)");
            
            var cell6 = headerRow.CreateCell(5);
            cell6.SetCellValue(" Last Working Day\n( Format DD/MM/YYYY  or DD-MM-YYYY)");
            
            // Apply basic formatting
            var workbook = worksheet.Workbook;
            var headerStyle = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            headerStyle.SetFont(font);
            headerStyle.Alignment = NPOI.SS.UserModel.HorizontalAlignment.Center;
            headerStyle.VerticalAlignment = NPOI.SS.UserModel.VerticalAlignment.Center;
            headerStyle.WrapText = true;
            
            // Apply style to all header cells
            for (int i = 0; i < 6; i++)
            {
                headerRow.GetCell(i).CellStyle = headerStyle;
            }
            
            // Set column widths
            worksheet.SetColumnWidth(0, 15 * 256); // IP Number
            worksheet.SetColumnWidth(1, 30 * 256); // IP Name
            worksheet.SetColumnWidth(2, 25 * 256); // No of Days
            worksheet.SetColumnWidth(3, 20 * 256); // Total Monthly Wages
            worksheet.SetColumnWidth(4, 30 * 256); // Reason Code
            worksheet.SetColumnWidth(5, 20 * 256); // Last Working Day
            
            // Set row height for header
            headerRow.Height = (short)(50 * 20); // Height in twips (1/20th of a point)
        }

        // Find data start row in NPOI worksheet
        private int FindDataStartRowNPOI(ISheet worksheet)
        {
            try
            {
                // Look for the first empty row after headers
                // Typically headers are in row 0 or 1, data starts from row 1 or 2
                for (int rowNum = 0; rowNum <= 5; rowNum++)
                {
                    var row = worksheet.GetRow(rowNum);
                    if (row == null) continue;
                    
                    var firstCell = row.GetCell(0);
                    if (firstCell != null)
                    {
                        var cellValue = firstCell.StringCellValue?.Trim()?.ToLower();
                        
                        // Check if this row contains headers like "ip number", "ip name", etc.
                        if (!string.IsNullOrEmpty(cellValue) && 
                            (cellValue.Contains("ip number") || cellValue.Contains("ip name") || 
                             cellValue.Contains("days") || cellValue.Contains("wages")))
                        {
                            return rowNum + 1; // Data starts after header row
                        }
                    }
                }
                
                // If no headers found, assume data starts from row 1
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding data start row: {ex.Message}");
                return 1; // Default to row 1
            }
        }
    }

    public class ECRCalculationResult
    {
        public decimal EPFWages { get; set; }
        public decimal EPSWages { get; set; }
        public decimal EDLIWages { get; set; }
        public decimal EEShare { get; set; }
        public decimal EPSContribution { get; set; }
        public decimal ERShare { get; set; }
        public decimal Refund { get; set; }
    }

    public class PayrollUploadRequest
    {
        public int CompanyId { get; set; }
        public int PayrollMonthId { get; set; }
        public IFormFile File { get; set; }
    }

    public class CreatePayrollEntryRequest
    {
        public int EmployeeId { get; set; }
        public int PayrollMonthId { get; set; }
        public decimal WorkingDays { get; set; } // Changed from int to decimal
        public decimal BasicDA { get; set; }
        public decimal GrossSalary { get; set; }
        public int Reason { get; set; }  // Changed from string to int
    }

    public class UpdatePayrollEntryRequest
    {
        public decimal WorkingDays { get; set; } // Changed from int to decimal
        public decimal BasicDA { get; set; }
        public decimal GrossSalary { get; set; }
        public int Reason { get; set; }  // Changed from string to int
    }
}
