using System;

namespace CreativeGroupsAPI.Models
{
    public class PayrollMonth
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public Company Company { get; set; }
        public string Month { get; set; }
        public int TotalDays { get; set; } = 30;
    }
}
