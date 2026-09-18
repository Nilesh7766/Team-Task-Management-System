using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Web.Models.Entities;

namespace TaskManagement.Web.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        var section = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var minutes = int.TryParse(section["ExpiryMinutes"], out var m) ? m : 120;
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        if (user.TeamId.HasValue)
            claims.Add(new Claim("teamId", user.TeamId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: section["Issuer"],
            audience: section["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
