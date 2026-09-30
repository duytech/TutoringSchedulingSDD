using BrightPath.Domain;

namespace BrightPath.Application.Common;

/// <summary>Why a use case was refused. Each kind maps to exactly one HTTP response in the Api.</summary>
public abstract record ResultError;

/// <summary>The input is wrong, field by field (400 ValidationProblem).</summary>
public sealed record ValidationError(IDictionary<string, string[]> Errors) : ResultError;

/// <summary>The input is well formed but makes no sense as a whole (400 Problem).</summary>
public sealed record BadRequestError(string Title, string Detail) : ResultError;

/// <summary>What the request points at does not exist (404 Problem).</summary>
public sealed record NotFoundError(string Title, string Detail) : ResultError;

/// <summary>The request breaks centre rules, all listed at once (409 Problem with <c>conflicts</c>).</summary>
public sealed record ConflictError(IReadOnlyList<ScheduleViolation> Conflicts) : ResultError;
