using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RankingDigi.Data;
using RankingDigi.Models;
using RankingDigi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
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

            if (!user.EmailConfirmed && user.Role != "Admin")
                return Unauthorized(new { error = "Confirme seu e-mail antes de fazer login. Verifique sua caixa de entrada." });

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
                Username       = dto.Username.Trim(),
                Role           = dto.Role,
                PlayerId       = dto.PlayerId,
                EmailConfirmed = true, // contas criadas pelo admin não precisam de confirmação
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

        // POST api/auth/forgot-password  — envia link de recuperação de senha
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto, [FromServices] IEmailService emailService)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { error = "Informe o e-mail cadastrado." });

            // Sempre retorna OK para não revelar se o e-mail existe
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == dto.Email.Trim().ToLower());

            if (user != null)
            {
                var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                user.PasswordResetToken       = token;
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);
                await _context.SaveChangesAsync();

                var baseUrl    = _config["AppSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5297";
                var resetUrl   = $"{baseUrl}/reset-password.html?token={token}";
                var html       = EmailTemplates.PasswordReset(user.Username, resetUrl);

                try { await emailService.SendEmailAsync(user.Email!, "Redefinir senha — RankingDigi", html); }
                catch { /* falha silenciosa para não revelar o e-mail */ }
            }

            return Ok(new { message = "Se esse e-mail estiver cadastrado, você receberá um link para redefinir a senha." });
        }

        // POST api/auth/reset-password  — redefine senha via token
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Token))
                return BadRequest(new { error = "Token inválido." });
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 4)
                return BadRequest(new { error = "A nova senha deve ter ao menos 4 caracteres." });
            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { error = "As senhas não coincidem." });

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.PasswordResetToken == dto.Token);

            if (user == null)
                return BadRequest(new { error = "Link inválido ou já utilizado." });
            if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
                return BadRequest(new { error = "Link expirado. Solicite um novo." });

            user.PasswordHash             = _hasher.HashPassword(user, dto.NewPassword);
            user.PasswordResetToken       = null;
            user.PasswordResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Senha redefinida com sucesso! Você já pode fazer login." });
        }

        // POST api/auth/register  — autoregistro público com confirmação por e-mail
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto, [FromServices] IEmailService emailService)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest(new { error = "Informe um nome de usuário." });
            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
                return BadRequest(new { error = "Informe um e-mail válido." });
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 4)
                return BadRequest(new { error = "A senha deve ter ao menos 4 caracteres." });
            if (dto.Password != dto.ConfirmPassword)
                return BadRequest(new { error = "As senhas não coincidem." });

            var usernameTaken = await _context.AppUsers
                .AnyAsync(u => u.Username.ToLower() == dto.Username.Trim().ToLower());
            if (usernameTaken)
                return Conflict(new { error = "Esse nome de usuário já está em uso." });

            var emailTaken = await _context.AppUsers
                .AnyAsync(u => u.Email != null && u.Email.ToLower() == dto.Email.Trim().ToLower());
            if (emailTaken)
                return Conflict(new { error = "Esse e-mail já está cadastrado." });

            // Cria jogador vinculado se solicitado
            Player? player = null;
            if (!string.IsNullOrWhiteSpace(dto.PlayerName))
            {
                player = new Player { Name = dto.PlayerName.Trim(), Score = 0 };
                _context.Players.Add(player);
                await _context.SaveChangesAsync();
            }

            // Cria usuário
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var user = new AppUser
            {
                Username                    = dto.Username.Trim(),
                Email                       = dto.Email.Trim().ToLower(),
                Role                        = "Player",
                EmailConfirmed              = false,
                EmailConfirmationToken      = token,
                EmailConfirmationTokenExpiry = DateTime.UtcNow.AddHours(24),
                PlayerId                    = player?.Id,
            };
            user.PasswordHash = _hasher.HashPassword(user, dto.Password);

            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            // Envia e-mail de confirmação
            var baseUrl = _config["AppSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5297";
            var confirmUrl = $"{baseUrl}/confirm-email.html?token={token}";
            var html = EmailTemplates.Confirmation(user.Username, confirmUrl);

            try
            {
                await emailService.SendEmailAsync(user.Email, "Confirme seu cadastro — RankingDigi", html);
            }
            catch (Exception ex)
            {
                // Remove registros criados se o e-mail falhar
                _context.AppUsers.Remove(user);
                if (player != null) _context.Players.Remove(player);
                await _context.SaveChangesAsync();
                return StatusCode(500, new { error = $"Não foi possível enviar o e-mail de confirmação: {ex.Message}" });
            }

            return Ok(new { message = "Cadastro realizado! Verifique seu e-mail para confirmar a conta." });
        }

        // GET api/auth/confirm-email?token=xxx
        [HttpGet("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { error = "Token inválido." });

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.EmailConfirmationToken == token);

            if (user == null)
                return BadRequest(new { error = "Token inválido ou já utilizado." });

            if (user.EmailConfirmationTokenExpiry < DateTime.UtcNow)
                return BadRequest(new { error = "Token expirado. Faça um novo cadastro." });

            user.EmailConfirmed               = true;
            user.EmailConfirmationToken       = null;
            user.EmailConfirmationTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "E-mail confirmado com sucesso! Você já pode fazer login." });
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

    public class ForgotPasswordDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        public string Token         { get; set; } = string.Empty;
        public string NewPassword   { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RegisterDto
    {
        public string Username        { get; set; } = string.Empty;
        public string Email           { get; set; } = string.Empty;
        public string Password        { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string? PlayerName     { get; set; }
    }

    public static class EmailTemplates
    {
        public static string PasswordReset(string username, string resetUrl)
        {
            var safeUsername = WebUtility.HtmlEncode(username);
            return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:#0b1020;font-family:'Inter',Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
    <tr><td align=""center"" style=""padding:40px 16px;"">
      <table width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background:#131929;border-radius:16px;overflow:hidden;border:1px solid #1e2a45;"">
        <tr><td style=""background:linear-gradient(135deg,#ff5d73,#ffb547);padding:32px;text-align:center;"">
          <div style=""font-size:2.5rem;"">🔑</div>
          <h1 style=""margin:8px 0 0;color:#fff;font-size:1.5rem;font-weight:700;"">RankingDigi</h1>
        </td></tr>
        <tr><td style=""padding:36px 32px;"">
          <h2 style=""margin:0 0 12px;color:#e2e8f7;font-size:1.15rem;"">Olá, {safeUsername}!</h2>
          <p style=""margin:0 0 24px;color:#8892aa;line-height:1.6;"">
            Recebemos uma solicitação para redefinir a senha da sua conta no <strong style=""color:#6d6fff;"">RankingDigi</strong>.<br>
            Clique no botão abaixo para criar uma nova senha.
          </p>
          <div style=""text-align:center;margin:28px 0;"">
            <a href=""{resetUrl}"" style=""display:inline-block;padding:14px 32px;background:linear-gradient(135deg,#ff5d73,#ffb547);color:#fff;text-decoration:none;border-radius:10px;font-weight:600;font-size:1rem;"">
              🔒 Redefinir senha
            </a>
          </div>
          <p style=""margin:0;color:#5a6480;font-size:0.82rem;text-align:center;"">
            Este link expira em <strong>2 horas</strong>.<br>
            Se você não solicitou isso, ignore este e-mail — sua senha permanece a mesma.
          </p>
        </td></tr>
        <tr><td style=""padding:16px 32px;border-top:1px solid #1e2a45;text-align:center;"">
          <p style=""margin:0;color:#3d4a65;font-size:0.75rem;"">RankingDigi &copy; {DateTime.UtcNow.Year}</p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }

        public static string Confirmation(string username, string confirmUrl)
        {
            var safeUsername = WebUtility.HtmlEncode(username);
            return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:#0b1020;font-family:'Inter',Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
    <tr><td align=""center"" style=""padding:40px 16px;"">
      <table width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background:#131929;border-radius:16px;overflow:hidden;border:1px solid #1e2a45;"">
        <!-- Header -->
        <tr><td style=""background:linear-gradient(135deg,#6d6fff,#00c2ff);padding:32px;text-align:center;"">
          <div style=""font-size:2.5rem;"">🏆</div>
          <h1 style=""margin:8px 0 0;color:#fff;font-size:1.5rem;font-weight:700;"">RankingDigi</h1>
        </td></tr>
        <!-- Body -->
        <tr><td style=""padding:36px 32px;"">
          <h2 style=""margin:0 0 12px;color:#e2e8f7;font-size:1.15rem;"">Olá, {safeUsername}! 👋</h2>
          <p style=""margin:0 0 24px;color:#8892aa;line-height:1.6;"">
            Seu cadastro no <strong style=""color:#6d6fff;"">RankingDigi</strong> foi criado com sucesso.<br>
            Clique no botão abaixo para confirmar seu e-mail e ativar sua conta.
          </p>
          <div style=""text-align:center;margin:28px 0;"">
            <a href=""{confirmUrl}"" style=""display:inline-block;padding:14px 32px;background:linear-gradient(135deg,#6d6fff,#00c2ff);color:#fff;text-decoration:none;border-radius:10px;font-weight:600;font-size:1rem;"">
              ✅ Confirmar e-mail
            </a>
          </div>
          <p style=""margin:0;color:#5a6480;font-size:0.82rem;text-align:center;"">
            Este link expira em <strong>24 horas</strong>.<br>
            Se você não criou essa conta, ignore este e-mail.
          </p>
        </td></tr>
        <!-- Footer -->
        <tr><td style=""padding:16px 32px;border-top:1px solid #1e2a45;text-align:center;"">
          <p style=""margin:0;color:#3d4a65;font-size:0.75rem;"">RankingDigi &copy; {DateTime.UtcNow.Year}</p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}
