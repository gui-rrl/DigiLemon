namespace RankingDigi.DTOs
{
    public class TournamentDto
    {
        public int Id { get; set; }
        public string ?Name { get; set; }
        public DateTime StartDate { get; set; }
        public int Status { get; set; }
        public string? InviteCode { get; set; }
        public List<BracketDto> ?Brackets { get; set; }
    }
}
