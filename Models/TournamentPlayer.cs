namespace RankingDigi.Models
{
    public class TournamentPlayer
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public int PlayerId { get; set; }
        public string ?Deck { get; set; }

        // Navegação
        public Tournament ?Tournament { get; set; }
        public Player ?Player { get; set; }
    }
}
