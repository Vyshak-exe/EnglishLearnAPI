using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using EnglishLearnApi.Data;
using EnglishLearnApi.DTOs;
using EnglishLearnApi.Models;
using Microsoft.Extensions.Configuration;
using EnglishLearnApi.DTOs.Responses;
using EnglishLearnApi.DTOs.Requests;

namespace EnglishLearnApi.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public AuthService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            if (await _db.Users.AnyAsync(u => u.Email == request.Email || u.Username == request.Username))
                throw new InvalidOperationException("Email or Username already exists.");

            var saltBytes = GenerateSalt(16);
            var hash = HashPassword(request.Password, saltBytes);

            var user = new User
            {
                Email = request.Email,
                Username = request.Username,
                PasswordSalt = Convert.ToBase64String(saltBytes),
                PasswordHash = hash,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return await CreateAuthResponse(user);
        }
        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = await _db.Users
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(u => u.Email == request.EmailOrUsername || u.Username == request.EmailOrUsername);

            if (user == null) throw new UnauthorizedAccessException("Invalid credentials");

            var salt = Convert.FromBase64String(user.PasswordSalt);
            var hashed = HashPassword(request.Password, salt);

            if (hashed != user.PasswordHash) throw new UnauthorizedAccessException("Invalid credentials");

            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return await CreateAuthResponse(user);
        }
        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
        {
            var token = await _db.RefreshTokens.Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == refreshToken);

            if (token == null || token.IsRevoked || token.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Invalid or expired refresh token");

            // revoke existing token
            token.IsRevoked = true;

            await _db.SaveChangesAsync();

            // create new pair
            return await CreateAuthResponse(token.User);
        }
        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
            if (token == null) return;
            token.IsRevoked = true;
            await _db.SaveChangesAsync();
        }

        // --- helpers ---
        private async Task<AuthResponse> CreateAuthResponse(User user)
        {
            var jwt = GenerateJwtToken(user, out DateTime jwtExpiresAt);
            var refresh = GenerateRefreshToken();
            var rt = new RefreshToken
            {
                Token = refresh,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(_config.GetValue<int>("JwtSettings:RefreshTokenExpirationDays"))
            };

            _db.RefreshTokens.Add(rt);
            await _db.SaveChangesAsync();

            return new AuthResponse
            {
                AccessToken = jwt,
                AccessTokenExpiresAt = jwtExpiresAt,
                RefreshToken = refresh,
                RefreshTokenExpiresAt = rt.ExpiresAt,
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email
            };
        }

        private string GenerateJwtToken(User user, out DateTime expiresAt)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secret = jwtSettings.GetValue<string>("Secret");
            var issuer = jwtSettings.GetValue<string>("Issuer");
            var audience = jwtSettings.GetValue<string>("Audience");
            var minutes = jwtSettings.GetValue<int>("AccessTokenExpirationMinutes");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            expiresAt = DateTime.UtcNow.AddMinutes(minutes);

            var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt, signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private static byte[] GenerateSalt(int size = 16)
        {
            var salt = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        private static string HashPassword(string password, byte[] salt)
        {
            // PBKDF2 with SHA256, 100k iterations, 32-byte key
            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            var key = deriveBytes.GetBytes(32);
            return Convert.ToBase64String(key);
        }
    }
}
