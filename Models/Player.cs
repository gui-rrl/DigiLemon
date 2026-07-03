namespace RankingDigi.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Score { get; set; } // pontuação da temporada atual
        public int CareerScore { get; set; } // pontuação geral, nunca reseta
        public string? AvatarUrl { get; set; }
    }

}
