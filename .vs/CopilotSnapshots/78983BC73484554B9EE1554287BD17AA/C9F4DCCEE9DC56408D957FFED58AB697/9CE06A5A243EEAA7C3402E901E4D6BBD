using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Vocentra.Models;

namespace Vocentra.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Job> Jobs => Set<Job>();
        public DbSet<Applicant> Applicants => Set<Applicant>();

        // Settings
        public DbSet<Vocentra.Models.Setting> Settings => Set<Vocentra.Models.Setting>();
        public DbSet<Vocentra.Models.UserSetting> UserSettings => Set<Vocentra.Models.UserSetting>();
        public DbSet<Vocentra.Models.UserProfile> UserProfiles => Set<Vocentra.Models.UserProfile>();
        public DbSet<Vocentra.Models.CompanyProfile> CompanyProfiles => Set<Vocentra.Models.CompanyProfile>();
        public DbSet<Vocentra.Models.SecuritySetting> SecuritySettings => Set<Vocentra.Models.SecuritySetting>();

        // NEW tables for one-click apply system
        public DbSet<UserApplicationProfile> UserApplicationProfiles => Set<UserApplicationProfile>();
        public DbSet<Application> Applications => Set<Application>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
