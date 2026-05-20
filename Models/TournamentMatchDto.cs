namespace RankingDigi.Models
{
    public class TournamentMatchDto
    {
        public int Id { get; set; }
        public int BracketId { get; set; }
        public int? Player1Id { get; set; }
        public int? Player2Id { get; set; }
        public string? Player1Deck { get; set; }
        public string? Player2Deck { get; set; }
        public int? WinnerId { get; set; }
        public int? NextMatchId { get; set; }
        public int? NextMatchPosition { get; set; }
        public DateTime? Date { get; set; }
        public bool IsPlayed { get; set; }
    }
}
