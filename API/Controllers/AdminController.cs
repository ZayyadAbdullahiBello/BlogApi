using System;
using API.DTOs;
using API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpPost("users")]
    public async Task<ActionResult<object>> CreateUser([FromBody] CreateUserRequest req)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(req.Email)) return ValidationProblem("Email is required.");
        if (string.IsNullOrWhiteSpace(req.Password)) return ValidationProblem("Password is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName)) return ValidationProblem("DisplayName is required.");
        if (string.IsNullOrWhiteSpace(req.Role)) return ValidationProblem("Role is required.");

        var role = req.Role.Trim();
        if (role is not ("Admin" or "Author"))
            return ValidationProblem("Role must be 'Admin' or 'Author'.");

        if (!await _roleManager.RoleExistsAsync(role))
            return Problem(statusCode: 500, detail: $"Role '{role}' does not exist. Check seeding.");

        var existing = await _userManager.FindByEmailAsync(req.Email.Trim());
        if (existing != null) return Conflict(new { message = "Email already exists." });

        var user = new AppUser
        {
            UserName = req.Email.Trim(),
            Email = req.Email.Trim(),
            DisplayName = req.DisplayName.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, req.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Failed to create user.",
                errors = createResult.Errors.Select(e => new { e.Code, e.Description })
            });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "User created, but failed to assign role.",
                errors = roleResult.Errors.Select(e => new { e.Code, e.Description })
            });
        }

        return Created($"/api/v1/admin/users/{user.Id}", new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            role
        });
    }
}
