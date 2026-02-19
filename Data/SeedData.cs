using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vocentra.Models;

namespace Vocentra.Data
{
    public static class SeedData
    {
        public static async Task EnsureSeedAsync(IServiceProvider services, IConfiguration cfg)
        {
            using var scope = services.CreateScope();
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            string[] roles = new[] { "Admin", "FinanceAdmin", "Moderator" };
            foreach (var r in roles)
            {
                if (!await roleMgr.RoleExistsAsync(r))
                {
                    await roleMgr.CreateAsync(new IdentityRole(r));
                }
            }

            var adminEmail = cfg["Seed:AdminEmail"];
            var adminPassword = cfg["Seed:AdminPassword"];

            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var user = await userMgr.FindByEmailAsync(adminEmail);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                    var r = await userMgr.CreateAsync(user, adminPassword);
                    if (r.Succeeded)
                    {
                        await userMgr.AddToRolesAsync(user, roles);
                    }
                }
                else
                {
                    // ensure roles
                    foreach (var role in roles)
                    {
                        if (!await userMgr.IsInRoleAsync(user, role))
                            await userMgr.AddToRoleAsync(user, role);
                    }
                }
            }

            // Add a sample bank account if none exists
            if (await db.PaymentBankAccounts.AnyAsync() == false)
            {
                db.PaymentBankAccounts.Add(new PaymentBankAccount
                {
                    BankName = "Capitec Bank",
                    AccountHolder = "Vocentra",
                    AccountNumber = "1886474298",
                    BranchCode = "470010",
                    IsActive = true,
                    ValidFrom = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
