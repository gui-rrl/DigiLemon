using RankingDigi.Models;

namespace RankingDigi.DTOs
{
    public class BracketDto
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public string ?Name { get; set; }
        public int Round { get; set; }
        public int Order { get; set; }
        public List<TournamentMatchDto>? Matches { get; set; }
    }
}
