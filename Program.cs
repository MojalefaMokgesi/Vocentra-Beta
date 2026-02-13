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

builder.Services.AddHttpContextAccessor();

// Connection string
var conn = builder.Configuration.GetConnectionString("DefaultConnection");

// Decide provider based on connection string contents
// Azure SQL / SQL Server usually contains "Server=" or "Data Source=" + "Initial Catalog="
// SQLite usually contains "Data Source=" + ".db" or ends with ".db"
var isSqlServer =
    !string.IsNullOrWhiteSpace(conn) &&
    (conn.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
     conn.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
     conn.Contains("User ID=", StringComparison.OrdinalIgnoreCase) ||
     conn.Contains("Password=", StringComparison.OrdinalIgnoreCase));

var isSqlite =
    !string.IsNullOrWhiteSpace(conn) &&
    (conn.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
     conn.Trim().EndsWith(".db", StringComparison.OrdinalIgnoreCase));

// Default behavior: if connection string is missing, use SQLite local file
if (string.IsNullOrWhiteSpace(conn))
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "vocentra.db");
    conn = $"Data Source={dbPath}";
    isSqlite = true;
    isSqlServer = false;
}

// Register DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (isSqlServer)
    {
        options.UseSqlServer(conn, sql =>
        {
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
    }
    else if (isSqlite)
    {
        // Normalize SQLite "vocentra.db" -> "Data Source=...\vocentra.db"
        if (conn.Trim().EndsWith(".db", StringComparison.OrdinalIgnoreCase) &&
            !conn.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            var dbPath = Path.Combine(builder.Environment.ContentRootPath, conn.Trim());
            conn = $"Data Source={dbPath}";
        }

        options.UseSqlite(conn);
    }
    else
    {
        throw new InvalidOperationException(
            "DefaultConnection is present but doesn't look like SQL Server or SQLite. " +
            "Set a valid SQL Server (Azure SQL) or SQLite connection string.");
    }
});

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

// Services
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<SettingsService>();

var app = builder.Build();

// Auto-migrate database (safe logging)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupMigrations");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        logger.LogInformation("Database migration completed successfully.");
    }
    catch (Exception ex)
    {
        // This makes the failure visible in Log Stream / stdout logs
        logger.LogCritical(ex, "Database migration failed during startup.");
        throw; // keep throwing so you don't run a broken app silently
    }
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
