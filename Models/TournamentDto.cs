namespace RankingDigi.DTOs
{
    public class TournamentDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime StartDate { get; set; }
        public int Status { get; set; }
        public string? InviteCode { get; set; }
        public int MaxPlayers { get; set; }
        public int Mode { get; set; }
        public int Format { get; set; }
        public int SwissRounds { get; set; }
        public int TopCutSize { get; set; }
        public int CurrentSwissRound { get; set; }
        public List<BracketDto>? Brackets { get; set; }
        public string? WinnerName { get; set; }
        public string? WinnerAvatarUrl { get; set; }
    }
}
