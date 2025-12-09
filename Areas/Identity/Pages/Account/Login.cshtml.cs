using System.ComponentModel.DataAnnotations;
using GrokBlazorApp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GrokBlazorApp.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ErrorMessage { get; set; } = "";

        public class InputModel
        {
            [Required]
            public string Email { get; set; } = "";

            [Required]
            public string Password { get; set; } = "";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, false, false);
            if (result.Succeeded)
            {
                // Check if user must change password
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user != null && user.MustChangePassword)
                {
                    return Redirect("/Identity/Account/Manage/ChangePassword");
                }
                return Redirect("/");
            }
            else
            {
                ErrorMessage = "Invalid login attempt.";
                return Page();
            }
        }
    }
}
