using System.Security.Claims;
using Business.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers
{
    // Shared plumbing for all API controllers: caller identity from the JWT and the
    // single MyResult-failure → ProblemDetails (RFC 7807) mapping. Every error body
    // on the wire is a ProblemDetails; the frontend reads its `detail` field.
    // "standard" rate-limit policy applies to every controller by default (inherited);
    // overridden per-method where a stricter/independent policy is needed (e.g. auth endpoints).
    [EnableRateLimiting("standard")]
    public abstract class ApiControllerBase : ControllerBase
    {
        // The caller's own id, taken from the access token (never from the URL).
        protected int? CallerId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        protected string CallerRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? "";

        // take the failed MyResult and map it to a ProblemDetails response, with the correct HTTP status code.
        protected ActionResult MapFailure<T>(MyResult<T> result,
            int unauthorizedStatusCode = StatusCodes.Status403Forbidden)
        {
            string detail = ErrorDetail(result);
            return result.FailureType switch
            {
                ErrorType.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, detail: detail),
                ErrorType.BadRequest => Problem(statusCode: StatusCodes.Status400BadRequest, detail: detail),
                ErrorType.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, detail: detail),
                ErrorType.Unauthorized => Problem(statusCode: unauthorizedStatusCode, detail: detail),
                // ErrorType.Failure messages are service-authored strings (repos swallow
                // exceptions, so raw exception text never lands here — the exception
                // middleware handles those with a generic body). Safe to surface, and the
                // frontend reads `detail` (e.g. "Account was created... Please log in.").
                _ => Problem(statusCode: StatusCodes.Status500InternalServerError,
                        detail: string.IsNullOrWhiteSpace(detail) ? "An unexpected error occurred." : detail)
            };
        }

        // Guard response for a JWT that somehow lacks a usable NameIdentifier claim.
        protected ActionResult MissingIdentity() =>
            Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid or missing user identity.");

        private static string ErrorDetail<T>(MyResult<T> result) =>
            string.Join(" ", result.Errors.Select(e => e.Message));
    
    }
}
