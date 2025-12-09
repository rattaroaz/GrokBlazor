using System.ComponentModel.DataAnnotations;
using GrokBlazorApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GrokBlazorApp.Areas.Identity.Pages.Account
{
    [Authorize(Roles = "Administrator")]
    public class AdminModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [BindProperty]
        public string NewGuestEmail { get; set; } = "";

        [BindProperty]
        public string NewGuestPassword { get; set; } = "";

        public string AddMessage { get; set; } = "";

        public List<ApplicationUser> Guests { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadGuests();
        }

        public async Task<IActionResult> OnPostAddGuestAsync()
        {
            await LoadGuests();

            if (string.IsNullOrWhiteSpace(NewGuestEmail) || string.IsNullOrWhiteSpace(NewGuestPassword))
            {
                AddMessage = "Email and password required.";
                return Page();
            }

            var user = new ApplicationUser 
            { 
                UserName = NewGuestEmail, 
                Email = NewGuestEmail,
                EmailConfirmed = true 
            };
            
            var result = await _userManager.CreateAsync(user, NewGuestPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Guest");
                AddMessage = "Guest added successfully.";
                NewGuestEmail = "";
                NewGuestPassword = "";
                await LoadGuests();
            }
            else
            {
                AddMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return Page();
        }

        public async Task<IActionResult> OnPostRemoveGuestAsync(string email)
        {
            await LoadGuests();

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                await _userManager.RemoveFromRoleAsync(user, "Guest");
                await LoadGuests();
            }

            return Page();
        }

        private async Task LoadGuests()
        {
            var role = await _roleManager.FindByNameAsync("Guest");
            if (role != null)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync("Guest");
                Guests = usersInRole.ToList();
            }
        }
    }
}
