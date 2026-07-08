using System.Net;

namespace CardsApi.Application.Common;

public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }

    public ApiException(HttpStatusCode statusCode, string errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public static ApiException NotFound(string message = "Resource not found.")
        => new(HttpStatusCode.NotFound, "NOT_FOUND", message);

    public static ApiException Forbidden(string message = "You do not have access to this resource.")
        => new(HttpStatusCode.Forbidden, "FORBIDDEN", message);

    public static ApiException BadRequest(string message)
        => new(HttpStatusCode.BadRequest, "BAD_REQUEST", message);

    public static ApiException Unauthorized(string message = "Invalid or expired credentials.")
        => new(HttpStatusCode.Unauthorized, "UNAUTHORIZED", message);
}
