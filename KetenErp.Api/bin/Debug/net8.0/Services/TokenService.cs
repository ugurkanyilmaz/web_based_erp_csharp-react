using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KetenErp.Infrastructure.Identity;
using KetenErp.Infrastructure.Data;
using KetenErp.Core.Entities;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KetenErp.Api.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly KetenErpDbContext _db;

        public TokenService(IConfiguration config, UserManager<ApplicationUser> userManager, KetenErpDbContext db)
        {
            _config = config;
            _userManager = userManager;
            _db = db;
        }

        // returns (accessToken, refreshToken)
        public async Task<(string accessToken, string refreshToken)> CreateTokensAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? ""),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
                new Claim(ClaimTypes.NameIdentifier, user.Id ?? "")
            };

            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "please-change-this-secret"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            // Access token lifetime (minutes) - default to 720 minutes (12h) if not configured
            var accessMinutes =  _config.GetValue<int?>("Jwt:AccessTokenExpirationMinutes") ?? 720;

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(accessMinutes),
                signingCredentials: creds);

            var access = new JwtSecurityTokenHandler().WriteToken(token);

            // Create refresh token
            var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var refreshDays = _config.GetValue<int?>("Jwt:RefreshTokenExpirationDays") ?? 30;
            var rt = new RefreshToken
            {
                Token = refreshToken,
                UserId = user.Id ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
            };
            _db.RefreshTokens.Add(rt);
            await _db.SaveChangesAsync();

            return (access, refreshToken);
        }
    }
}
