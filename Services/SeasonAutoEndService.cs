using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;

namespace RankingDigi.Services
{
    // Verifica periodicamente se a temporada ativa já passou da data de término
    // (encerra à meia-noite do dia seguinte à EndDate) e a encerra automaticamente.
    public class SeasonAutoEndService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SeasonAutoEndService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

        public SeasonAutoEndService(IServiceScopeFactory scopeFactory, ILogger<SeasonAutoEndService> logger)
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
                    await CheckAndCloseExpiredSeasonsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao verificar temporadas expiradas.");
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

        private async Task CheckAndCloseExpiredSeasonsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RankingContext>();

            var now = DateTime.UtcNow;

            // Encerra à meia-noite (00:00) do dia seguinte à data de término planejada
            var expiredSeasons = await context.Seasons
                .Where(s => s.IsActive && now >= s.EndDate.Date.AddDays(1))
                .ToListAsync();

            foreach (var season in expiredSeasons)
            {
                _logger.LogInformation("Encerrando automaticamente a temporada '{Name}' (Id={Id}).", season.Name, season.Id);
                await SeasonService.CloseSeasonAsync(context, season);
            }
        }
    }
}
