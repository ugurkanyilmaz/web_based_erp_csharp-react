using KetenErp.Api.Services;
using KetenErp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace KetenErp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenService _tokenService;
    private readonly KetenErp.Infrastructure.Data.KetenErpDbContext _db;
    private readonly IConfiguration _config;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, TokenService tokenService, KetenErp.Infrastructure.Data.KetenErpDbContext db, IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _db = db;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            var user = new ApplicationUser { UserName = dto.UserName, Email = dto.Email, FullName = dto.FullName };
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            if (!string.IsNullOrEmpty(dto.Role))
            {
                await _userManager.AddToRoleAsync(user, dto.Role);
            }

            return Ok(new { user.Id, user.UserName });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.UserName);
            if (user == null) return Unauthorized();

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded) return Unauthorized();

            var (access, refresh) = await _tokenService.CreateTokensAsync(user);

            // Set refresh token as HttpOnly cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // change to true when running under https
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(_config.GetValue<int?>("Jwt:RefreshTokenExpirationDays") ?? 30)
            };
            Response.Cookies.Append("refreshToken", refresh, cookieOptions);

            return Ok(new { token = access });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            // Read refresh token from cookie
            if (!Request.Cookies.TryGetValue("refreshToken", out var existingRefresh) || string.IsNullOrEmpty(existingRefresh))
                return Unauthorized();

            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == existingRefresh);
            if (rt == null) return Unauthorized();
            if (rt.RevokedAt != null) return Unauthorized();
            if (rt.ExpiresAt < DateTime.UtcNow) return Unauthorized();

            var user = await _userManager.FindByIdAsync(rt.UserId);
            if (user == null) return Unauthorized();

            // Revoke the old refresh token and issue a new pair
            rt.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var (newAccess, newRefresh) = await _tokenService.CreateTokensAsync(user);

            // Mark old token replaced
            rt.ReplacedByToken = newRefresh;
            await _db.SaveChangesAsync();

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(_config.GetValue<int?>("Jwt:RefreshTokenExpirationDays") ?? 30)
            };
            Response.Cookies.Append("refreshToken", newRefresh, cookieOptions);

            return Ok(new { token = newAccess });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue("refreshToken", out var existingRefresh) && !string.IsNullOrEmpty(existingRefresh))
            {
                var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == existingRefresh);
                if (rt != null && rt.RevokedAt == null)
                {
                    rt.RevokedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
            // Remove cookie
            Response.Cookies.Delete("refreshToken");
            return Ok();
        }

        public record RegisterDto(string UserName, string Email, string Password, string? Role, string? FullName);
        public record LoginDto(string UserName, string Password);
    }
}
