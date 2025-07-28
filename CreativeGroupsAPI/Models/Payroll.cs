using System;

namespace CreativeGroupsAPI.Models
{
    public class PayrollUpload
    {
        public int CompanyId { get; set; }
        public string payrollMonth { get; set; }
        public IFormFile  file { get; set; }
    }
    public class Payroll
    {
        public int Id { get; set; }
        public int PayrollMonthid { get; set; }
        public int WorkingDays { get; set; }
        public decimal BasicDA { get; set; }
        public decimal GrossSalary { get; set; }
        public int NCP { get; set; }
        public PayrollMonth PayrollMonth { get; set; }
        public string Reason { get; set; }
    }
}
