namespace RankingDigi.Models
{
    public class MatchResultDto
    {
        public int WinnerId { get; set; }
        public int? LoserId { get; set; } // pode ser nulo em casos de bye

        // Placar em games da melhor de 3 (opcional). Vitória: 2x0 ou 2x1; empate: 1x1.
        public int? WinnerGames { get; set; }
        public int? LoserGames { get; set; }

    }
}
