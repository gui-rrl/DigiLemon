namespace RankingDigi.Models
{
    public class Bracket
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public string ?Name { get; set; } // "Oitavas de final", "Quartas", etc.
        public int Round { get; set; }   // 1 = oitavas, 2 = quartas, 3 = semi, 4 = final
        public int Order { get; set; }   // ordem de exibição
        public Tournament ?Tournament { get; set; }
        public ICollection<TournamentMatch> ?Matches { get; set; }
    }
}
