using Microsoft.EntityFrameworkCore;
using RankingDigi.Data;
using RankingDigi.Models;

namespace RankingDigi.Services
{
    /// <summary>
    /// Gera e resolve os códigos que o simulador DCGO usa para reportar resultados.
    /// Um código por slot de jogador, para identificar a partida E quem está reportando.
    /// </summary>
    public class MatchReportCodeService
    {
        // Mesmo alfabeto do InviteCode: sem 0/O e 1/I/L, que os jogadores confundem ao digitar.
        private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        // Mais longo que o InviteCode (8) de propósito: um convite é feito para ser
        // compartilhado, um código de relato é credencial portadora. 31^10 ≈ 8,2×10^14.
        private const int CodeLength = 10;

        private readonly RankingContext _context;

        public MatchReportCodeService(RankingContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Garante que toda partida "reportável" de um torneio online tenha os dois códigos.
        ///
        /// Geração preguiçosa na leitura em vez de instrumentar os ~7 pontos que atribuem
        /// Player1Id/Player2Id (geração de chave, rodada Swiss, avanço de vencedor, queda pro
        /// lower bracket): um choke point só, que se auto-cura a cada novo estado do torneio e
        /// funciona retroativamente. Mesmo padrão do backfill de InviteCode em GetTournaments.
        /// </summary>
        public async Task EnsureCodesAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);

            // Status 2 = finalizado. Não gatear em "Status == 1" porque torneios de Dupla
            // Eliminação nunca chegam a esse estado (só o Swiss seta Status = 1).
            if (tournament == null || tournament.Mode != 1 || tournament.Status == 2) return;

            var pendentes = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId
                         && !m.IsPlayed
                         && !m.IsBye
                         && m.Player1Id != null
                         && m.Player2Id != null
                         && (m.Player1ReportCode == null || m.Player2ReportCode == null))
                .ToListAsync();

            // Caso comum a cada refresh de página: nada a fazer, nenhuma escrita.
            if (pendentes.Count == 0) return;

            // Todos os códigos já em uso, numa consulta só. Antes era um AnyAsync por tentativa
            // de código (2 por partida), o que numa rodada inteira virava dezenas de idas ao
            // banco. Agora a checagem de colisão acontece em memória.
            var usados = new HashSet<string>(StringComparer.Ordinal);
            var existentes = await _context.TournamentMatches
                .Where(m => m.Player1ReportCode != null || m.Player2ReportCode != null)
                .Select(m => new { m.Player1ReportCode, m.Player2ReportCode })
                .ToListAsync();
            foreach (var e in existentes)
            {
                if (e.Player1ReportCode != null) usados.Add(e.Player1ReportCode);
                if (e.Player2ReportCode != null) usados.Add(e.Player2ReportCode);
            }

            foreach (var match in pendentes)
            {
                if (match.Player1ReportCode == null) match.Player1ReportCode = GenerateUniqueCode(usados);
                if (match.Player2ReportCode == null) match.Player2ReportCode = GenerateUniqueCode(usados);
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Localiza a partida e o slot a partir de um código. Normaliza a entrada porque o
        /// jogador digita isso à mão (pode vir com hífen, espaço ou minúscula).
        /// </summary>
        public async Task<(TournamentMatch Match, int Slot)?> ResolveAsync(string? code)
        {
            var normalizado = Normalize(code);
            if (string.IsNullOrEmpty(normalizado)) return null;

            var match = await _context.TournamentMatches
                .FirstOrDefaultAsync(m => m.Player1ReportCode == normalizado
                                       || m.Player2ReportCode == normalizado);
            if (match == null) return null;

            return (match, match.Player1ReportCode == normalizado ? 1 : 2);
        }

        /// <summary>
        /// Troca os dois códigos de uma partida e descarta os relatos já feitos — regenerar
        /// significa recomeçar a confirmação do zero (usado quando um código vaza ou se perde).
        /// </summary>
        public async Task<(string Player1Code, string Player2Code)> RegenerateAsync(TournamentMatch match)
        {
            var usados = new HashSet<string>(StringComparer.Ordinal);
            var existentes = await _context.TournamentMatches
                .Where(m => m.Id != match.Id && (m.Player1ReportCode != null || m.Player2ReportCode != null))
                .Select(m => new { m.Player1ReportCode, m.Player2ReportCode })
                .ToListAsync();
            foreach (var e in existentes)
            {
                if (e.Player1ReportCode != null) usados.Add(e.Player1ReportCode);
                if (e.Player2ReportCode != null) usados.Add(e.Player2ReportCode);
            }

            match.Player1ReportCode = GenerateUniqueCode(usados);
            match.Player2ReportCode = GenerateUniqueCode(usados);

            var relatos = await _context.MatchReports
                .Where(r => r.TournamentMatchId == match.Id)
                .ToListAsync();
            if (relatos.Count > 0) _context.MatchReports.RemoveRange(relatos);

            await _context.SaveChangesAsync();
            return (match.Player1ReportCode!, match.Player2ReportCode!);
        }

        /// <summary>
        /// Estado de confirmação por partida, para as telas mostrarem "aguardando adversário"
        /// ou "relatos divergentes". Derivado das linhas de MatchReport + IsPlayed, sem coluna
        /// denormalizada — uma segunda fonte de verdade dessincronizaria na primeira resolução
        /// manual do admin. Só devolve partidas que têm algum relato.
        /// </summary>
        public async Task<Dictionary<int, string>> GetReportStatesAsync(int tournamentId)
        {
            var jogadas = await _context.TournamentMatches
                .Where(m => m.TournamentId == tournamentId && m.IsPlayed)
                .Select(m => m.Id)
                .ToListAsync();

            return await GetReportStatesAsync(tournamentId, jogadas.ToHashSet());
        }

        /// <summary>
        /// Mesma coisa, mas para quem já carregou as partidas do torneio — evita reconsultar
        /// só para descobrir quais estão jogadas.
        /// </summary>
        public async Task<Dictionary<int, string>> GetReportStatesAsync(int tournamentId, HashSet<int> idsDePartidasJogadas)
        {
            var relatos = await _context.MatchReports
                .Where(r => _context.TournamentMatches
                    .Any(m => m.Id == r.TournamentMatchId && m.TournamentId == tournamentId))
                .Select(r => new { r.TournamentMatchId, r.ClaimedWinnerTpId, r.ClaimedPlayer1GameWins, r.ClaimedPlayer2GameWins })
                .ToListAsync();

            if (relatos.Count == 0) return new Dictionary<int, string>();

            var jogadasSet = idsDePartidasJogadas;

            return relatos
                .GroupBy(r => r.TournamentMatchId)
                .ToDictionary(g => g.Key, g =>
                {
                    if (jogadasSet.Contains(g.Key)) return "resolved";
                    if (g.Count() < 2) return "awaiting";

                    // Dois relatos numa partida não aplicada só acontece quando divergem:
                    // se coincidissem, o segundo relato teria aplicado o resultado na hora.
                    var primeiro = g.First();
                    bool iguais = g.All(r => r.ClaimedWinnerTpId == primeiro.ClaimedWinnerTpId
                                          && r.ClaimedPlayer1GameWins == primeiro.ClaimedPlayer1GameWins
                                          && r.ClaimedPlayer2GameWins == primeiro.ClaimedPlayer2GameWins);
                    return iguais ? "awaiting" : "conflict";
                });
        }

        /// <summary>Formata para leitura humana: ABCDE-FGHIJ.</summary>
        public static string Format(string? code) =>
            string.IsNullOrEmpty(code) || code.Length != CodeLength
                ? (code ?? string.Empty)
                : $"{code[..5]}-{code[5..]}";

        public static string Normalize(string? code) =>
            string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : code.Trim().ToUpperInvariant()
                      .Replace("-", "").Replace(" ", "").Replace(".", "");

        /// <param name="usados">
        /// Códigos já ocupados — vem do banco (as duas colunas, porque os índices únicos são por
        /// coluna e não impediriam o mesmo código no slot 1 de uma partida e no slot 2 de outra)
        /// mais os gerados neste lote, que ainda não foram salvos.
        /// </param>
        private static string GenerateUniqueCode(HashSet<string> usados)
        {
            var rng = Random.Shared;
            for (int tentativa = 0; tentativa < 8; tentativa++)
            {
                var code = new string(Enumerable.Range(0, CodeLength)
                    .Select(_ => Alphabet[rng.Next(Alphabet.Length)]).ToArray());

                if (usados.Add(code)) return code;   // Add devolve false se já existia
            }

            var fallback = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            usados.Add(fallback);
            return fallback;
        }
    }
}
