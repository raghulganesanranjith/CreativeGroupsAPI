using System;
using System.ComponentModel.DataAnnotations;

namespace CreativeGroupsAPI.Models
{
    public class PayrollUploadStaging
    {
        public int Id { get; set; }
        public string UploadId { get; set; }
        public int CompanyId { get; set; }
        public int PayrollMonthId { get; set; }
        public int RowNumber { get; set; }
        
        // Employee identification
        public string PFNumber { get; set; }
        public string ESINumber { get; set; }
        public string Name { get; set; }
        
        // Payroll data
        public int WorkingDays { get; set; }
        public decimal BasicDA { get; set; }
        public decimal GrossSalary { get; set; }
        public int NCP { get; set; }
        public string Reason { get; set; } = "0";
        
        // Status fields
        public string Error { get; set; }
        public bool Committed { get; set; } = false;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        
        // Navigation properties
        public Company Company { get; set; }
        public PayrollMonth PayrollMonth { get; set; }
    }
}
