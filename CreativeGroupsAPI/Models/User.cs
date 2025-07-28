using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CreativeGroupsAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; }
        
        [Required]
        public UserRole Role { get; set; }
        
        // For Organization and User roles - which organization they belong to
        public int? OrganizationId { get; set; }
        public Organization? Organization { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        private DateTime _createdDate = DateTime.UtcNow;
        [Column(TypeName = "timestamp with time zone")]
        public DateTime CreatedDate 
        { 
            get => _createdDate;
            set => _createdDate = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        }
    }

    public enum UserRole
    {
        Admin = 1,
        Organization = 2,
        User = 3
    }
}
