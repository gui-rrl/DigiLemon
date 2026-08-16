namespace RankingDigi.Models
{
    /// <summary>
    /// Um dos 1-3 decks candidatos que um participante enviou para o sorteio (Tournament.DeckMode
    /// 1 ou 2), antes do torneio iniciar. Linhas efêmeras: existem só enquanto o sorteio não
    /// rodou — TournamentDeckDrawService as apaga assim que resolve o DeckId definitivo de cada
    /// participante. A mera existência de uma linha aqui já significa "este deck está pendente de
    /// sorteio", usado por DeckController.IsDeckLockedAsync pra travar edição/exclusão.
    /// </summary>
    public class TournamentPlayerDeckOption
    {
        public int Id { get; set; }
        public int TournamentPlayerId { get; set; }
        public int DeckId { get; set; }

        public TournamentPlayer? TournamentPlayer { get; set; }
        public Deck? Deck { get; set; }
    }
}
