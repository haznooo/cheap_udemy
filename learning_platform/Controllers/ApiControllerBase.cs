using System.Security.Claims;
using Business.Common;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    // Shared plumbing for all API controllers: caller identity from the JWT and the
    // single MyResult-failure → ProblemDetails (RFC 7807) mapping. Every error body
    // on the wire is a ProblemDetails; the frontend reads its `detail` field.
    public abstract class ApiControllerBase : ControllerBase
    {
        // The caller's own id, taken from the access token (never from the URL).
        protected int? CallerId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        protected string CallerRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? "";

        // A service-level Unauthorized failure means "authenticated but not allowed"
        // (ownership/enrollment checks), so it maps to 403 by default — 401 is reserved
        // for missing/invalid credentials, which SPA interceptors treat as "re-login".
        // The authentication endpoints pass 401 explicitly (failed login IS a credential
        // problem there).
        protected ActionResult MapFailure<T>(MyResult<T> result,
            int unauthorizedStatusCode = StatusCodes.Status403Forbidden) =>
            result.FailureType switch
            {
                ErrorType.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, detail: ErrorDetail(result)),
                ErrorType.BadRequest => Problem(statusCode: StatusCodes.Status400BadRequest, detail: ErrorDetail(result)),
                ErrorType.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, detail: ErrorDetail(result)),
                ErrorType.Unauthorized => Problem(statusCode: unauthorizedStatusCode, detail: ErrorDetail(result)),
                _ => Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "An unexpected error occurred.")
            };

        // Guard response for a JWT that somehow lacks a usable NameIdentifier claim.
        protected ActionResult MissingIdentity() =>
            Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid or missing user identity.");

        private static string ErrorDetail<T>(MyResult<T> result) =>
            string.Join(" ", result.Errors.Select(e => e.Message));
    }
}
