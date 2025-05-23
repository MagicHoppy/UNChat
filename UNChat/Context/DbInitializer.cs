using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using UNChat.Context;
using UNChat.Models;

public static class DbInitializer
{
    public static void Initialize(UNChatDbContext context, IServiceProvider serviceProvider)
    {
        context.Database.EnsureCreated();

        // Add default emojis if none exist
        if (!context.Emojis.Any())
        {
            var emojis = new Emoji[]
            {
                new Emoji { Symbol = "😀" },
                new Emoji { Symbol = "😂" },
                new Emoji { Symbol = "😍" },
                new Emoji { Symbol = "😎" },
                new Emoji { Symbol = "😢" },
                new Emoji { Symbol = "🤔" },
                new Emoji { Symbol = "🎉" },
                new Emoji { Symbol = "❤️" },
                new Emoji { Symbol = "👍" },
                new Emoji { Symbol = "👏" }
            };

            context.Emojis.AddRange(emojis);
            context.SaveChanges();
        }

        // Add roles and admin user
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

        string adminRole = "Administrator";

        // Create role if it doesn't exist
        if (!roleManager.RoleExistsAsync(adminRole).Result)
        {
            var role = new IdentityRole(adminRole);
            roleManager.CreateAsync(role).Wait();
        }

        // Create default admin user
        var adminEmail = "admin@unchat.com";
        var adminUser = userManager.FindByEmailAsync(adminEmail).Result;

        if (adminUser == null)
        {
            adminUser = new User
            {
                UserName = adminEmail,
                Email = adminEmail,
                Name = "Admin",
                EmailConfirmed = true,
                IsOnline = false
            };

            var result = userManager.CreateAsync(adminUser, "Admin123!").Result;
            if (result.Succeeded)
            {
                userManager.AddToRoleAsync(adminUser, adminRole).Wait();
            }
        }
    }
}
