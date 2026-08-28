using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAI.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Ensure Database and Tables exist with retry handling
            int retryCount = 0;
            bool dbReady = false;
            while (!dbReady && retryCount < 3)
            {
                try
                {
                    retryCount++;
                    if (await dbContext.Database.CanConnectAsync())
                    {
                        await dbContext.Database.MigrateAsync();
                        dbReady = true;
                    }
                    else
                    {
                        await dbContext.Database.EnsureCreatedAsync();
                        dbReady = true;
                    }
                }
                catch
                {
                    if (retryCount >= 3)
                    {
                        // Final fallback attempt
                        try { await dbContext.Database.EnsureCreatedAsync(); } catch { }
                        dbReady = true;
                    }
                    else
                    {
                        await Task.Delay(1500); // Wait for MySQL service to respond
                    }
                }
            }

            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Seed Roles
            string[] roleNames = { "Admin", "Farmer", "Customer" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Single Default Admin User
            string adminEmail = "admin123@gmail.com";
            string adminPassword = "Admin@123";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "System",
                    LastName = "Admin",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
            else
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);
                await userManager.ResetPasswordAsync(adminUser, token, adminPassword);

                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 3. Seed Default Categories if empty
            if (await dbContext.Database.CanConnectAsync() && !await dbContext.Categories.AnyAsync())
            {
                var defaultCategories = new List<Category>
                {
                    new Category { Name = "Vegetables", Description = "Fresh green organic vegetables", CreatedAt = DateTime.UtcNow },
                    new Category { Name = "Fruits", Description = "Fresh seasonal farm fruits", CreatedAt = DateTime.UtcNow },
                    new Category { Name = "Grains", Description = "Quality wheat, rice, pulses, and food grains", CreatedAt = DateTime.UtcNow },
                    new Category { Name = "Dairy", Description = "Pure milk, butter, ghee, and dairy products", CreatedAt = DateTime.UtcNow },
                    new Category { Name = "Fertilizers", Description = "Organic fertilizers and crop boosters", CreatedAt = DateTime.UtcNow },
                    new Category { Name = "Seeds", Description = "High yield quality farm seeds", CreatedAt = DateTime.UtcNow },
                    new Category { Name = "Equipment", Description = "Modern farming tools and irrigation equipment", CreatedAt = DateTime.UtcNow }
                };

                await dbContext.Categories.AddRangeAsync(defaultCategories);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
