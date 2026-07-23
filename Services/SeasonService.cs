using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    public static class SeasonService
    {
        // Arquiva a pontuação final de cada jogador e zera o ranking da temporada.
        // Usado tanto pelo encerramento manual (Admin) quanto pelo encerramento automático.
        public static async Task CloseSeasonAsync(RankingContext context, Season season)
        {
            var players = await context.Players.ToListAsync();
            foreach (var player in players)
            {
                context.PlayerSeasonScores.Add(new PlayerSeasonScore
                {
                    SeasonId = season.Id,
                    PlayerId = player.Id,
                    FinalScore = player.Score,
                    FinalScoreOnline = player.ScoreOnline,
                });
                player.Score = 0;
                player.ScoreOnline = 0;
            }

            season.ClosedAt = DateTime.UtcNow;
            season.IsActive = false;

            await context.SaveChangesAsync();
        }
    }
}
