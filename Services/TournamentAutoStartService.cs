using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    /// <summary>
    /// Verifica periodicamente torneios de vagas "Indefinido" (MaxPlayers == 0) cujo prazo de
    /// inscrição já venceu, e os inicia automaticamente — mesma lógica do botão manual "Iniciar
    /// torneio" em tournament-setup.html (SwissService.StartAsync / TournamentService.
    /// GenerateDoubleElimination), só que disparada pelo relógio em vez de um clique de admin.
    ///
    /// Só age em torneios Indefinido: torneios com vagas fixas continuam exigindo início manual,
    /// como sempre foi.
    /// </summary>
    public class TournamentAutoStartService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TournamentAutoStartService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

        public TournamentAutoStartService(IServiceScopeFactory scopeFactory, ILogger<TournamentAutoStartService> logger)
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
                    await CheckAndStartExpiredRegistrationsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao verificar torneios com inscrições vencidas.");
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

        private async Task CheckAndStartExpiredRegistrationsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RankingContext>();

            var now = DateTime.UtcNow;

            // Mesma convenção de "meia-noite do dia seguinte" do TournamentAutoEndService, para o
            // prazo de inscrição se comportar de forma consistente com a data de término.
            var candidates = await context.Tournaments
                .Where(t => t.Status == 0
                         && t.MaxPlayers == 0
                         && t.RegistrationDeadline != null
                         && now >= t.RegistrationDeadline!.Value.Date.AddDays(1))
                .ToListAsync();

            if (candidates.Count == 0) return;

            foreach (var tournament in candidates)
            {
                var participantIds = await context.TournamentPlayers
                    .Where(tp => tp.TournamentId == tournament.Id)
                    .Select(tp => tp.Id)
                    .ToListAsync();

                // Mesmo guard mínimo do SwissService.StartAsync — sem tentar e sem quebrar o loop,
                // só fica aguardando o admin resolver na mão (adicionar participantes ou encerrar
                // manualmente de outro jeito).
                if (participantIds.Count < 2)
                {
                    _logger.LogInformation(
                        "Torneio '{Name}' (Id={Id}) tem prazo de inscrição vencido mas só {Count} participante(s) — aguardando ação manual.",
                        tournament.Name, tournament.Id, participantIds.Count);
                    continue;
                }

                try
                {
                    if (tournament.Format == 0)
                    {
                        // Dupla Eliminação exige contagem par — mesma regra do endpoint manual
                        // (GenerateDoubleElimination). Ímpar aqui não é erro: só fica pendente até
                        // o admin ajustar a lista e clicar em "Iniciar torneio".
                        if (participantIds.Count % 2 != 0)
                        {
                            _logger.LogInformation(
                                "Torneio '{Name}' (Id={Id}) tem prazo vencido mas {Count} participante(s) (ímpar) — Dupla Eliminação exige par. Aguardando ação manual.",
                                tournament.Name, tournament.Id, participantIds.Count);
                            continue;
                        }

                        var tournamentService = scope.ServiceProvider.GetRequiredService<TournamentService>();
                        await tournamentService.GenerateDoubleElimination(tournament.Id, participantIds);
                    }
                    else
                    {
                        var swissService = scope.ServiceProvider.GetRequiredService<SwissService>();
                        await swissService.StartAsync(tournament.Id);
                    }

                    _logger.LogInformation(
                        "Torneio '{Name}' (Id={Id}) iniciado automaticamente ao vencer o prazo de inscrição, com {Count} participante(s).",
                        tournament.Name, tournament.Id, participantIds.Count);
                }
                catch (Exception ex)
                {
                    // Um torneio problemático não pode impedir os outros de serem processados.
                    _logger.LogError(ex, "Falha ao iniciar automaticamente o torneio '{Name}' (Id={Id}).", tournament.Name, tournament.Id);
                }
            }
        }
    }
}
