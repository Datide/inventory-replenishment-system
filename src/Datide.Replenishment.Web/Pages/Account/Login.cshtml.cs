using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Infrastructure.Persistence;
using Datide.Replenishment.Web.Resources;
using Datide.Replenishment.Web.Services;

namespace Datide.Replenishment.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly ReplenishmentDbContext _db;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly LoginNotifier _notifier;

    public LoginModel(
        ReplenishmentDbContext db,
        IStringLocalizer<SharedResource> localizer,
        LoginNotifier notifier)
    {
        _db = db;
        _localizer = localizer;
        _notifier = notifier;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = _localizer["Username and password are required."];
            return Page();
        }

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == Username && u.IsActive);

        if (user is null)
        {
            ErrorMessage = _localizer["Invalid username or password."];
            return Page();
        }

        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, Password);
        if (result != PasswordVerificationResult.Success)
        {
            ErrorMessage = _localizer["Invalid username or password."];
            return Page();
        }

        // Build the auth cookie with role claim so [Authorize(Roles=...)] works.
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });

        // Fire-and-forget owner notification, captured off the request thread so
        // the send can never block or fail the login (LoginNotifier never throws).
        var loginNotification = new LoginNotification(
            user.Username,
            user.Role.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        _ = Task.Run(() => _notifier.NotifyLogin(loginNotification));

        return RedirectToPage("/Index");
    }
}
