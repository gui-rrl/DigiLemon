namespace RankingDigi.Models
{
    // Foto da pontuação de um jogador ao final de uma temporada encerrada
    public class PlayerSeasonScore
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public int PlayerId { get; set; }
        public int FinalScore { get; set; }
        public int FinalScoreOnline { get; set; }
    }
}
