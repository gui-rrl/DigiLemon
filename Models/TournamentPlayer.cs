namespace RankingDigi.Models
{
    public class TournamentPlayer
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }

        /// <summary>
        /// Jogador registrado no sistema. Nulo quando o participante é um convidado (via link).
        /// </summary>
        public int? PlayerId { get; set; }

        /// <summary>
        /// Nome do participante convidado (via link de convite). Nulo para jogadores registrados.
        /// </summary>
        public string? GuestName { get; set; }

        public string? Deck { get; set; }

        // Navegação
        public Tournament? Tournament { get; set; }
        public Player? Player { get; set; }

        /// <summary>Retorna o nome a exibir independentemente de ser guest ou jogador registrado.</summary>
        public string DisplayName => GuestName ?? Player?.Name ?? "Desconhecido";
    }
}
