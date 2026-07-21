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
    }
}
