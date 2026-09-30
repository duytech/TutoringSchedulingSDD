namespace BrightPath.Domain;

public sealed class Student
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
}
