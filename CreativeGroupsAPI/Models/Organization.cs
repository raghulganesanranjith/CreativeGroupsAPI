using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CreativeGroupsAPI.Models
{
    public class Organization
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        private DateTime _createdDate = DateTime.UtcNow;
        [Column(TypeName = "timestamp with time zone")]
        public DateTime CreatedDate 
        { 
            get => _createdDate;
            set => _createdDate = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        }
        
        // Navigation properties
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Company> Companies { get; set; } = new List<Company>();
    }
}
