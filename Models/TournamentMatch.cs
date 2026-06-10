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
        public Tournament ?LoserGoesToMatch { get; set; }
        public TournamentMatch ?NextMatch { get; set; }

    }
}
