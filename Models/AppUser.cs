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

        /// <summary>
        /// Opcional: vínculo com o registro de jogador correspondente.
        /// Permite que um usuário com Role=Player acesse apenas seu próprio perfil.
        /// </summary>
        public int? PlayerId { get; set; }
        public Player? Player { get; set; }
    }
}
