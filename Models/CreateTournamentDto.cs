public class CreateTournamentDto
{
    public string? Name { get; set; }
    public DateTime StartDate { get; set; }
    public List<PlayerDeckDto>? Players { get; set; }
    public int MaxPlayers { get; set; }
    public int Format { get; set; } = 0;      // 0=DoubleElim, 1=Swiss
    public int TopCutSize { get; set; } = 8;  // 4 ou 8
}
