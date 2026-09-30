namespace BrightPath.Application.Abstractions;

/// <summary>Which overlap the database refused.</summary>
public enum SlotKind
{
    Room,
    Tutor,
    Student,
}

/// <summary>
/// A race got past the code checks: someone else took the slot between our read and our insert, and an
/// exclusion constraint refused the write.
/// </summary>
public sealed class SlotTakenException(SlotKind slot, Exception inner)
    : Exception($"The {slot.ToString().ToLowerInvariant()} slot was taken by another booking.", inner)
{
    public SlotKind Slot { get; } = slot;
}
