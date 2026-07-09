namespace RankingDigi.Models
{
    public class DeckCard
    {
        public int Id { get; set; }
        public int DeckId { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsDigiEgg { get; set; } // true = zona do deck de Digi-Egg, false = deck principal
    }
}
