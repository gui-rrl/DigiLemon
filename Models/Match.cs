namespace RankingDigi.Models
{
    public class Match
    {
        public int Id { get; set; }
        public int Player1Id { get; set; }
        public int Player2Id { get; set; }
        public int WinnerId { get; set; } // 0 para empate
        public DateTime Date { get; set; }
        public string ?Deck1 { get; set; }
        public string ?Deck2 { get; set; }
        public int? SeasonId { get; set; } // temporada vigente no momento do registro (nula para partidas antigas)
    }
}
