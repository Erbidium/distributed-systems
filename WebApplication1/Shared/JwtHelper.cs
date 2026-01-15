using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Shared;

public static class JwtHelper
{
    public static TokenValidationParameters Parameters =>
        new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SUPER_SECRET_KEY"))
        };

    public static string Generate()
    {
        var token = new JwtSecurityToken(
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("SUPER_SECRET_KEY")),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

