namespace RankingDigi.Models
{
    public class JoinTournamentDto
    {
        public int DeckId { get; set; }         // DeckMode == 0 (comportamento normal)
        public List<int>? DeckIds { get; set; } // DeckMode 1 ou 2 — deve ter DeckPoolSize decks
    }
}
