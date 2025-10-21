using Microsoft.AspNetCore.Identity;

namespace KetenErp.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
    }
}
