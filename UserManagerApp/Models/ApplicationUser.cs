using Microsoft.AspNetCore.Identity;

namespace UserManagerApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public bool IsApproved { get; set; } = false;
    }
}
