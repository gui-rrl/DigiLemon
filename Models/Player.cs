namespace RankingDigi.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Score { get; set; } // pontuação da temporada atual (presencial)
        public int CareerScore { get; set; } // pontuação geral, nunca reseta (presencial)
        public int ScoreOnline { get; set; } // pontuação da temporada atual (online, simulador DCGO)
        public int CareerScoreOnline { get; set; } // pontuação geral online, nunca reseta
        public string? AvatarUrl { get; set; }
    }

}
