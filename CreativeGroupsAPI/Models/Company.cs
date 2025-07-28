using System.Collections.Generic;

namespace CreativeGroupsAPI.Models
{
    public class Company
    {
        public int CompanyId { get; set; }
        public string Name { get; set; }
        public bool PFEnabled { get; set; }
        public bool ESIEnabled { get; set; }
        
        // Organization relationship
        public int? OrganizationId { get; set; }
        public Organization? Organization { get; set; }
    }
}
