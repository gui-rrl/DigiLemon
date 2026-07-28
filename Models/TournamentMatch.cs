namespace RankingDigi.Models
{
    public class TournamentMatch
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public int MatchType { get; set; }   // 0=Upper, 1=Lower, 2=GrandFinal
        public int Round { get; set; }       // 1,2,3... dentro do bracket
        public int? Player1Id { get; set; }   // pode ser nulo se vaga ainda não definida
        public int? Player2Id { get; set; }
        public int? WinnerId { get; set; }
        public int? LoserGoesToMatchId { get; set; } // partida no lower bracket (para perdedor)
        public int? NextMatchId { get; set; } // referência para a próxima partida no chaveamento (se houver)
        public int? NextMatchPosition { get; set; } // indica se este vencedor vai para o "slot" esquerdo ou direito da próxima partida
        public DateTime? Date { get; set; }
        public bool IsPlayed { get; set; }
        public bool IsBye { get; set; } = false;

        /// <summary>
        /// Placar em games da partida (melhor de 3): ex. 2x0, 2x1 ou 1x1 (empate).
        /// Nulo em partidas antigas, registradas antes de existir placar por game, e em byes
        /// (byes contam como 2x0 por convenção, resolvido no cálculo dos desempates).
        /// </summary>
        public int? Player1GameWins { get; set; }
        public int? Player2GameWins { get; set; }

        /// <summary>
        /// Códigos que o simulador DCGO usa para reportar o resultado — um por slot de jogador,
        /// para o código identificar tanto a partida quanto QUEM está reportando (o DCGO não tem
        /// contas, só um apelido digitado). São credenciais portadoras: cada jogador só enxerga o
        /// seu. Gerados sob demanda apenas em torneios online (Mode == 1) e só quando os dois
        /// slots já estão preenchidos — nulos em torneios presenciais, byes e partidas futuras.
        ///
        /// NUNCA devolver estes campos em endpoint anônimo (ver projeção em GetTournamentMatches).
        /// </summary>
        public string? Player1ReportCode { get; set; }
        public string? Player2ReportCode { get; set; }

        public Tournament ?LoserGoesToMatch { get; set; }
        public TournamentMatch ?NextMatch { get; set; }

    }
}
