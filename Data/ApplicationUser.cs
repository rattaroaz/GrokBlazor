using Microsoft.AspNetCore.Identity;

namespace GrokBlazorApp.Data;

public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Indicates whether the user must change their password on next login.
    /// </summary>
    public bool MustChangePassword { get; set; } = false;
}
