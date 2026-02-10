using System;

namespace API.DTOs;

public record CreateUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role // "Admin" or "Author"
);
