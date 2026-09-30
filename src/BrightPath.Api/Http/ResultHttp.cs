using BrightPath.Application.Common;

namespace BrightPath.Api.Http;

/// <summary>Turns a use case's <see cref="Result{T}"/> into the HTTP response. Each error kind has one response.</summary>
public static class ResultHttp
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.Error switch
        {
            null => onSuccess(result.Value!),
            ValidationError e => TypedResults.ValidationProblem(e.Errors),
            BadRequestError e => TypedResults.Problem(
                title: e.Title, detail: e.Detail, statusCode: StatusCodes.Status400BadRequest),
            NotFoundError e => TypedResults.Problem(
                title: e.Title, detail: e.Detail, statusCode: StatusCodes.Status404NotFound),
            ConflictError e => TypedResults.Problem(
                title: "The booking breaks centre rules",
                detail: e.Conflicts.Count == 1 ? "1 conflict." : $"{e.Conflicts.Count} conflicts.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["conflicts"] = e.Conflicts }),
            _ => throw new InvalidOperationException($"No HTTP response for {result.Error.GetType().Name}."),
        };
}
