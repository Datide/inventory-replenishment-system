using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Datide.Replenishment.Application.Abstractions;
using Datide.Replenishment.Application.Services;
using Datide.Replenishment.Domain.Interfaces;
using Datide.Replenishment.Domain.Rules;
using Datide.Replenishment.Infrastructure.Persistence;
using Datide.Replenishment.Web.Services;

// --- Use USD formatting everywhere (demo currency) regardless of host locale ---
// NB: UI culture is handled separately by the localization middleware below,
// but number/currency formatting stays USD across all languages.
var usd = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = usd;

var builder = WebApplication.CreateBuilder(args);

// --- Database (SQLite — zero setup; swap provider for SQL Server later) ---
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);
var connectionString = $"Data Source={Path.Combine(dataDir, "datide.db")}";

builder.Services.AddDbContext<ReplenishmentDbContext>(o =>
    o.UseSqlite(connectionString));

// --- Application services ---
builder.Services.AddScoped<IStockSnapshotRepository, StockSnapshotRepository>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IReplenishmentRule>(
    _ => new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 2));
builder.Services.AddScoped<ReplenishmentService>();

// --- Login activity log (local-only, credential-free; replaces the withdrawn SMTP notifier) ---
builder.Services.AddSingleton<LoginActivityLog>();

// --- Cookie authentication (username + password, role-based) ---
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// --- Localization (EN default; zh-Hant & zh-Hans selectable) ---
var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("zh-Hant"),
    new CultureInfo("zh-Hans"),
};
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
    // Prefer our language-selection cookie over browser detection.
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider { CookieName = ".DatideLang" },
        new AcceptLanguageHeaderRequestCultureProvider(),
    ];
});
builder.Services.AddLocalization();

builder.Services.AddRazorPages()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(Datide.Replenishment.Web.Resources.SharedResource)));

var app = builder.Build();

// --- Create + seed database on startup ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReplenishmentDbContext>();
    db.Database.EnsureCreated();
    DatabaseSeeder.Seed(db);
}

app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

// --- Language switcher endpoint: sets the culture cookie, then redirects back ---
app.MapGet("/set-language", (HttpContext http, string culture, string returnUrl) =>
{
    var supported = supportedCultures.Select(c => c.Name).ToHashSet();
    if (!supported.Contains(culture))
    {
        culture = "en";
    }

    var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
    http.Response.Cookies.Append(
        ".DatideLang",
        cookieValue,
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
        });

    return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
});

// --- Read-only demo activity endpoint (anonymous, low-information, credential-free) ---
// Exposes only: total login count, timestamps, public demo usernames and
// /24-masked IPs. No credentials, no full IPs. An external poller reads this.
app.MapGet("/demo-activity", (LoginActivityLog log) =>
{
    return Results.Text(log.GetActivitySummary(), "text/plain; charset=utf-8");
});

app.MapRazorPages();

app.Run();
