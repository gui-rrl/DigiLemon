public class CreateTournamentDto
{
    public string ?Name { get; set; }
    public DateTime StartDate { get; set; }
    public List<PlayerDeckDto> ?Players { get; set; }
    public int MaxPlayers { get; set; }
}
