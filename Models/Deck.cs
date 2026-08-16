namespace RankingDigi.Models
{
    public class Deck
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CoverCardNumber { get; set; } // carta escolhida como capa/papel de parede do deck na listagem
        public int? CoverTcgplayerId { get; set; } // arte específica da carta de capa (null = arte padrão)

        // Death Random: quando não-nulo, este deck é uma cópia automática recebida num sorteio
        // Death Random — SourceDeckId aponta pro deck original (de outro jogador), SourceTournamentId
        // pro torneio que gerou a cópia (usado só pro texto do selo/tooltip).
        public int? SourceDeckId { get; set; }
        public int? SourceTournamentId { get; set; }
    }
}
