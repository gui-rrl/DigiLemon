namespace RankingDigi.Models
{
    public class Season
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; } // data planejada de término (define a duração)
        public DateTime? ClosedAt { get; set; } // quando a temporada foi de fato encerrada (manual ou automática)
        public bool IsActive { get; set; }
    }
}
