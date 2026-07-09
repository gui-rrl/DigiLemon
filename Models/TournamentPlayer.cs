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
        public int? DeckId { get; set; } // deck salvo do jogador (opcional; nulo para convidados sem conta)

        // Swiss standings
        public int SwissPoints { get; set; } = 0;
        public int SwissWins   { get; set; } = 0;
        public int SwissLosses { get; set; } = 0;
        public int SwissDraws  { get; set; } = 0;

        // Navegação
        public Tournament? Tournament { get; set; }
        public Player? Player { get; set; }

        public string DisplayName => GuestName ?? Player?.Name ?? "Desconhecido";
    }
}
