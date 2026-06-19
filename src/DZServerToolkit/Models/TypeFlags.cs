namespace DZServerToolkit.Models;

public sealed class TypeFlags
{
    public bool CountInCargo { get; set; }
    public bool CountInHoarder { get; set; }
    public bool CountInMap { get; set; } = true;
    public bool CountInPlayer { get; set; }
    public bool Crafted { get; set; }
    public bool Deloot { get; set; }
}
