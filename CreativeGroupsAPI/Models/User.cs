using System.ComponentModel.DataAnnotations;

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
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public enum UserRole
    {
        Admin = 1,
        Organization = 2,
        User = 3
    }
}
