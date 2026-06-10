namespace RankingDigi.Models
{
    public class Tournament
    {
        public int Id { get; set; }
        public string ?Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Status { get; set; } // 0: em preparação, 1: em andamento, 2: finalizado
        public string? InviteCode { get; set; }
        public int MaxPlayers { get; set; } = 0;

        // Swiss
        public int Format { get; set; } = 0;          // 0=DoubleElim, 1=Swiss
        public int SwissRounds { get; set; } = 0;      // total de rodadas Swiss (calculado na criação)
        public int TopCutSize { get; set; } = 8;       // 4 ou 8
        public int CurrentSwissRound { get; set; } = 0; // 0 = não iniciado

        public ICollection<Bracket> Brackets { get; set; } = new List<Bracket>();
        public ICollection<TournamentPlayer>? TournamentPlayers { get; set; }
    }
}
