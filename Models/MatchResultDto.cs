namespace RankingDigi.Models
{
    public class MatchResultDto
    {
        public int WinnerId { get; set; }
        public int? LoserId { get; set; } // pode ser nulo em casos de bye

    }
}
