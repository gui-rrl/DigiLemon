namespace RankingDigi.Models
{
    /// <summary>
    /// Relato de resultado enviado pelo simulador DCGO. Existe no máximo uma linha por
    /// (partida, slot): o resultado só é aplicado de verdade quando os DOIS slots reportam
    /// e os relatos coincidem. Se divergirem, a partida fica em conflito para um organizador
    /// resolver na mão — é isso que impede um código vazado de alterar resultado sozinho.
    ///
    /// A revisão atualiza a linha existente (não é append-only): enquanto está pendente, quem
    /// errou pode corrigir, e a única direção em que faz sentido convergir é a verdade — o
    /// relator não vê o claim do adversário. RevisionCount/UpdatedAt preservam o rastro.
    /// </summary>
    public class MatchReport
    {
        public int Id { get; set; }
        public int TournamentMatchId { get; set; }

        /// <summary>1 ou 2 — qual slot da partida reportou.</summary>
        public int PlayerSlot { get; set; }

        /// <summary>
        /// TournamentPlayer que ocupava o slot no momento do relato. Serve para detectar o caso
        /// raro de a vaga ter sido remanejada depois que o código já havia sido gerado.
        /// </summary>
        public int ReporterTournamentPlayerId { get; set; }

        // ── Claim gravado em termos ABSOLUTOS ────────────────────────────────────────
        // O DCGO envia em termos relativos ("eu ganhei"), mas guardamos já normalizado.
        // Assim comparar os dois relatos vira uma igualdade simples e a tela de conflito
        // do admin não tem ambiguidade sobre quem afirmou o quê.

        /// <summary>Vencedor declarado (TournamentPlayer.Id). Nulo = empate declarado.</summary>
        public int? ClaimedWinnerTpId { get; set; }
        public int ClaimedPlayer1GameWins { get; set; }
        public int ClaimedPlayer2GameWins { get; set; }

        // ── Auditoria ────────────────────────────────────────────────────────────────
        /// <summary>Apelido livre digitado no DCGO — não é identidade confiável, só rastro.</summary>
        public string? ReporterNickname { get; set; }
        public string? ClientVersion { get; set; }
        public string? SourceIp { get; set; }

        /// <summary>Quantas vezes este slot corrigiu o próprio relato.</summary>
        public int RevisionCount { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public TournamentMatch? TournamentMatch { get; set; }
    }
}
