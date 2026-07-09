namespace RankingDigi.Models
{
    public class Deck
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
