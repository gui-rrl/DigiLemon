public class CreateTournamentDto
{
    public string? Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? RegistrationDeadline { get; set; } // último dia de inscrição (opcional)
    public List<PlayerDeckDto>? Players { get; set; }
    public int MaxPlayers { get; set; }
    public int Mode { get; set; } = 0;        // 0=Presencial, 1=Online (simulador DCGO)
    public int Format { get; set; } = 0;      // 0=DoubleElim, 1=Swiss
    public int TopCutSize { get; set; } = 8;  // 4 ou 8
    public int DeckMode { get; set; } = 0;    // 0=Normal, 1=Sorteio entre decks próprios, 2=Death Random
    public int DeckPoolSize { get; set; } = 1; // 1-3; só relevante quando DeckMode != 0
}
