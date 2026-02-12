using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vocentra.Data;
using Vocentra.Models;
using Vocentra.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Razor Pages (Identity UI)
#if DEBUG
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
#else
builder.Services.AddRazorPages();
#endif

// Optional but useful for services that need HttpContext
builder.Services.AddHttpContextAccessor();

// SQLite
var dbConn = builder.Configuration.GetConnectionString("DefaultConnection");

// If config contains only a file name (like "vocentra.db"), normalize it to a full path
if (string.IsNullOrWhiteSpace(dbConn))
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "vocentra.db");
    dbConn = $"Data Source={dbPath}";
}
else if (dbConn.Trim().EndsWith(".db", StringComparison.OrdinalIgnoreCase) && !dbConn.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    // handle cases like: "vocentra.db"
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, dbConn.Trim());
    dbConn = $"Data Source={dbPath}";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(dbConn));

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

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Pipeline
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

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
