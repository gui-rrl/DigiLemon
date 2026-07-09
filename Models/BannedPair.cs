namespace RankingDigi.Models
{
    // Regra de "par banido": se CardNumberA está no deck, CardNumberB não pode estar (e vice-versa).
    public class BannedPair
    {
        public int Id { get; set; }
        public string CardNumberA { get; set; } = string.Empty;
        public string CardNumberB { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
    }
}
