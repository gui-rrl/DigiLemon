namespace RankingDigi.Models
{
    public class Tournament
    {
        public int Id { get; set; }
        public string ?Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Status { get; set; } // 0: em preparação, 1: em andamento, 2: finalizado
        public string? InviteCode { get; set; } // código curto usado no link de convite
        public ICollection<Bracket> Brackets { get; set; } = new List<Bracket>();
        public ICollection<TournamentPlayer>? TournamentPlayers { get; set; }
    }
}
