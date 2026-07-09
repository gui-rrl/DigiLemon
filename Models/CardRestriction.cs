namespace RankingDigi.Models
{
    // Lista oficial de cartas banidas/restritas do Digimon Card Game.
    // MaxCopies = 0 -> banida (não pode entrar no deck); MaxCopies = 1..3 -> restrita a essa quantidade.
    public class CardRestriction
    {
        public int Id { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public int MaxCopies { get; set; }
        public DateTime EffectiveDate { get; set; }
    }
}
