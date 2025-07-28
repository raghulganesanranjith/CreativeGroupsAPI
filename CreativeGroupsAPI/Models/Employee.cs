using System;
using System.ComponentModel.DataAnnotations.Schema;

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
        
        private DateTime _joiningDate;
        [Column(TypeName = "timestamp with time zone")]
        public DateTime JoiningDate 
        { 
            get => _joiningDate;
            set => _joiningDate = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        }

        private DateTime? _leavingDate;
        [Column(TypeName = "timestamp with time zone")]
        public DateTime? LeavingDate 
        { 
            get => _leavingDate;
            set => _leavingDate = value.HasValue ? 
                (value.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value.Value.ToUniversalTime()) 
                : null;
        }
        
        public string PFNumber { get; set; }
        public string ESINumber { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Error { get; set; }
    }
}
