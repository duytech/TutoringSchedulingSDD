namespace BrightPath.Application.Common;

/// <summary>
/// What a use case returns: a value, or the one reason it was refused. The Api turns it into an HTTP response,
/// so the use cases never know about HTTP. A handler returns either a value or an error, and the implicit
/// conversions let it write <c>return view;</c> or <c>return new NotFoundError(...);</c>.
/// </summary>
public sealed class Result<T>
{
    private Result(T? value, ResultError? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>Set when <see cref="Error"/> is null.</summary>
    public T? Value { get; }

    public ResultError? Error { get; }

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(ResultError error) => new(default, error);
}
