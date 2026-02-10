using System;
using API.Models;

namespace API.Services;

public interface IJwtTokenService
{
    Task<string> CreateTokenAsync(AppUser user);
}
