namespace RankingDigi.Models
{
    /// <summary>
    /// Relato enviado pelo DCGO. O resultado vem em termos RELATIVOS ("eu ganhei"), não por id:
    /// o código já fixa qual partida e qual slot, então perguntar "você ganhou?" torna atribuição
    /// errada estruturalmente impossível — o DCGO não conhece TournamentPlayer.Id nem precisa.
    /// </summary>
    public class MatchReportDto
    {
        /// <summary>"win", "loss" ou "draw" — sempre do ponto de vista de quem reporta.</summary>
        public string? Outcome { get; set; }

        /// <summary>Games vencidos por quem reporta (melhor de 3).</summary>
        public int? YourGameWins { get; set; }

        /// <summary>Games vencidos pelo adversário.</summary>
        public int? OpponentGameWins { get; set; }

        /// <summary>Apelido digitado no DCGO. Só auditoria — não é identidade confiável.</summary>
        public string? ReporterNickname { get; set; }

        public string? ClientVersion { get; set; }
    }
}
