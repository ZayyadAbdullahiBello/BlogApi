using System;

namespace API.DTOs;

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token);
