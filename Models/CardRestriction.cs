namespace RankingDigi.Models
{
    // Lista oficial de cartas com limite de cópias diferente do padrão (4) no Digimon Card Game.
    // MaxCopies = 0 -> banida (não pode entrar no deck); MaxCopies = 1..3 -> restrita a essa quantidade;
    // MaxCopies > 4 -> carta cujo próprio texto permite incluir mais cópias que o padrão (ex.: EX11-027).
    public class CardRestriction
    {
        public int Id { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public int MaxCopies { get; set; }
        public DateTime EffectiveDate { get; set; }
    }
}
