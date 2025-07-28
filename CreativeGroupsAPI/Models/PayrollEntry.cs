using System;
using System.ComponentModel.DataAnnotations;

namespace CreativeGroupsAPI.Models
{
    public class PayrollEntry
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int CompanyId { get; set; }
        public int PayrollMonthId { get; set; }
        public decimal WorkingDays { get; set; }
        public decimal BasicDA { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal NCP { get; set; }  // Changed from int to decimal
        public int Reason { get; set; } = 0;  // Changed from string to int
        
        // Navigation properties
        public Employee Employee { get; set; }
        public Company Company { get; set; }
        public PayrollMonth PayrollMonth { get; set; }
    }
}
