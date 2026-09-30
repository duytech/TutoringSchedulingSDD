namespace BrightPath.Domain;

public sealed class Tutor
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Subject { get; set; }
}
