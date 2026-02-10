using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Vocentra.Data;
using Vocentra.Models;
using Vocentra.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

#if DEBUG
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
#else
builder.Services.AddRazorPages();
#endif

// IHttpClientFactory (needed for PayFast validate)
builder.Services.AddHttpClient();

// PayFast options + service
builder.Services.Configure<PayFastOptions>(builder.Configuration.GetSection("PayFast"));
builder.Services.AddScoped<PayFastService>();

// SQLite
var dbFile = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(dbFile))
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "vocentra.db");
    dbFile = $"Data Source={dbPath}";
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(dbFile));

// Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
});

// Your services
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<SettingsService>();

var app = builder.Build();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
