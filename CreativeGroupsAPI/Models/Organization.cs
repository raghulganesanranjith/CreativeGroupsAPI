using System.ComponentModel.DataAnnotations;

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
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        // Navigation properties
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Company> Companies { get; set; } = new List<Company>();
    }
}
