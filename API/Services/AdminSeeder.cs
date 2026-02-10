using System;
using API.Models;
using Microsoft.AspNetCore.Identity;

namespace API.Services;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, IConfiguration config)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<AppUser>>();

        var roles = new[] { "Admin", "Author"};
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        var displayName = config["SeedAdmin:DisplayName"] ?? "Admin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null) return;

        var admin = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded) return;

        await userManager.AddToRoleAsync(admin, "Admin");
    }
}
