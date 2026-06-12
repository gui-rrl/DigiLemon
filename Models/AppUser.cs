namespace RankingDigi.Models
{
    /// <summary>
    /// Usuário do sistema (login/permissões). Independente da tabela Players.
    /// Role: "Admin" = acesso total | "Player" = somente leitura + perfil próprio.
    /// </summary>
    public class AppUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Player";   // "Admin" | "Player"

        // E-mail e confirmação
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; } = true; // true = pode logar
        public string? EmailConfirmationToken { get; set; }
        public DateTime? EmailConfirmationTokenExpiry { get; set; }

        // Recuperação de senha
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }

        /// <summary>
        /// Opcional: vínculo com o registro de jogador correspondente.
        /// </summary>
        public int? PlayerId { get; set; }
        public Player? Player { get; set; }
    }
}
