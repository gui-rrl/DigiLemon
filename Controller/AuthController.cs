using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RankingDigi.Data;
using RankingDigi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RankingDigi.Controller
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly RankingContext _context;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<AppUser> _hasher = new();

        public AuthController(RankingContext context, IConfiguration config)
        {
            _context = context;
            _config  = config;
        }

        // POST api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Informe usuário e senha." });

            var user = await _context.AppUsers
                .Include(u => u.Player)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.Trim().ToLower());

            if (user == null)
                return Unauthorized(new { error = "Usuário ou senha inválidos." });

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { error = "Usuário ou senha inválidos." });

            var token = GenerateJwt(user);
            return Ok(new
            {
                token,
                username  = user.Username,
                role      = user.Role,
                playerId  = user.PlayerId,
                playerName = user.Player?.Name,
            });
        }

        // GET api/auth/me  (requer token)
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user   = await _context.AppUsers.Include(u => u.Player).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Role,
                user.PlayerId,
                playerName = user.Player?.Name,
            });
        }

        // POST api/auth/users  — cria novo usuário (apenas Admin)
        [HttpPost("users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Informe usuário e senha." });
            if (dto.Role != "Admin" && dto.Role != "Player")
                return BadRequest(new { error = "Role deve ser 'Admin' ou 'Player'." });

            var exists = await _context.AppUsers.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower());
            if (exists) return Conflict(new { error = "Já existe um usuário com esse nome." });

            var user = new AppUser
            {
                Username = dto.Username.Trim(),
                Role     = dto.Role,
                PlayerId = dto.PlayerId,
            };
            user.PasswordHash = _hasher.HashPassword(user, dto.Password);

            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.Username, user.Role, user.PlayerId });
        }

        // GET api/auth/users  — lista usuários (apenas Admin)
        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ListUsers()
        {
            var users = await _context.AppUsers
                .Include(u => u.Player)
                .Select(u => new { u.Id, u.Username, u.Role, u.PlayerId, PlayerName = u.Player != null ? u.Player.Name : null })
                .ToListAsync();
            return Ok(users);
        }

        // DELETE api/auth/users/{id}  — remove usuário (apenas Admin)
        [HttpDelete("users/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.AppUsers.FindAsync(id);
            if (user == null) return NotFound();
            _context.AppUsers.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT api/auth/users/{id}/password  — troca senha (Admin ou o próprio usuário)
        [HttpPut("users/{id}/password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto dto)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && currentUserId != id)
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 4)
                return BadRequest(new { error = "A nova senha deve ter ao menos 4 caracteres." });

            var user = await _context.AppUsers.FindAsync(id);
            if (user == null) return NotFound();

            user.PasswordHash = _hasher.HashPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Senha alterada com sucesso." });
        }

        // POST api/auth/change-password  — troca a própria senha (exige senha atual)
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangeOwnPassword([FromBody] ChangeOwnPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                return BadRequest(new { error = "Informe a senha atual." });
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 4)
                return BadRequest(new { error = "A nova senha deve ter ao menos 4 caracteres." });
            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { error = "As senhas não coincidem." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user   = await _context.AppUsers.FindAsync(userId);
            if (user == null) return NotFound();

            var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);
            if (verify == PasswordVerificationResult.Failed)
                return BadRequest(new { error = "Senha atual incorreta." });

            user.PasswordHash = _hasher.HashPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Senha alterada com sucesso." });
        }

        // ── JWT ──────────────────────────────────────────────────────────────────
        private string GenerateJwt(AppUser user)
        {
            var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = DateTime.UtcNow.AddHours(double.Parse(_config["Jwt:ExpiresInHours"]!));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,           user.Username),
                new Claim(ClaimTypes.Role,           user.Role),
                new Claim("playerId",                user.PlayerId?.ToString() ?? ""),
            };

            var token = new JwtSecurityToken(
                issuer:   _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims:   claims,
                expires:  expiry,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class CreateUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role     { get; set; } = "Player";
        public int?   PlayerId { get; set; }
    }

    public class ChangePasswordDto
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangeOwnPasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword     { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
