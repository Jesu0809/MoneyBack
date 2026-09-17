using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MoneyBack.Api.Config;
using MoneyBack.Api.Models;

namespace MoneyBack.Api.Services;

public record RefreshTokenGenerado(string TokenEnClaro, string TokenHash, DateTime ExpiraEn);

public class TokenService(IOptions<JwtOptions> jwtOptions)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public string GenerarAccessToken(Usuario usuario, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new("nombre", usuario.Nombre),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(rol => new Claim(ClaimTypes.Role, rol)));

        var clave = new SymmetricSecurityKey(Convert.FromBase64String(_jwt.ClaveSecreta));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutos),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshTokenGenerado GenerarRefreshToken()
    {
        var bytesAleatorios = RandomNumberGenerator.GetBytes(64);
        var tokenEnClaro = Convert.ToBase64String(bytesAleatorios);

        return new RefreshTokenGenerado(
            tokenEnClaro,
            HashearToken(tokenEnClaro),
            DateTime.UtcNow.AddDays(_jwt.RefreshTokenDias));
    }

    public static string HashearToken(string tokenEnClaro)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tokenEnClaro));
        return Convert.ToHexString(bytes);
    }
}
