using System.ComponentModel.DataAnnotations;
using DailyPilot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DailyPilot.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public List<SelectListItem> TimeZones { get; set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Display name")]
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Username")]
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9._@+\-]+$", ErrorMessage = "Username can use letters, numbers and . _ - @ + only (no spaces).")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Time zone")]
        public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        PopulateTimeZones();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        PopulateTimeZones();

        if (!ModelState.IsValid)
            return Page();

        var user = new ApplicationUser
        {
            UserName = Input.UserName,
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            TimeZoneId = Input.TimeZoneId,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            _logger.LogInformation("User created a new account with password.");
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }

    private void PopulateTimeZones()
    {
        TimeZones = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz => new SelectListItem(tz.DisplayName, tz.Id))
            .ToList();
    }
}
