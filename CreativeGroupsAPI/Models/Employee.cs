using System;

namespace CreativeGroupsAPI.Models
{
    public class EmployeeUpload
    {
        public int CompanyId { get; set; }

        public IFormFile formFile { get; set; }
    }
    public class Employee
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public Company Company { get; set; }
        public string Name { get; set; }
        public DateTime JoiningDate { get; set; }

        public DateTime? LeavingDate { get; set; }
        public string PFNumber { get; set; }
        public string ESINumber { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Error { get; set; }
    }
}
