using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;

namespace RankingDigi.Services
{
    // Verifica periodicamente se algum torneio já passou da data de término
    // (encerra à meia-noite do dia seguinte à EndDate) e o encerra automaticamente.
    // Vale para todos os formatos (Double Elim, Swiss+TopCut, Swiss Puro).
    public class TournamentAutoEndService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TournamentAutoEndService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

        public TournamentAutoEndService(IServiceScopeFactory scopeFactory, ILogger<TournamentAutoEndService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndCloseExpiredTournamentsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao verificar torneios expirados.");
                }

                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // aplicação está encerrando
                }
            }
        }

        private async Task CheckAndCloseExpiredTournamentsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RankingContext>();

            var now = DateTime.UtcNow;

            // Encerra à meia-noite (00:00) do dia seguinte à data de término escolhida na criação
            var expiredTournaments = await context.Tournaments
                .Where(t => t.Status != 2 && t.EndDate != null && now >= t.EndDate.Value.Date.AddDays(1))
                .ToListAsync();

            foreach (var tournament in expiredTournaments)
            {
                _logger.LogInformation("Encerrando automaticamente o torneio '{Name}' (Id={Id}).", tournament.Name, tournament.Id);
                tournament.Status = 2;
            }

            if (expiredTournaments.Count > 0)
                await context.SaveChangesAsync();
        }
    }
}
