using System.Net;
using CampusEats.Client.Models;

namespace CampusEats.Client.Services;

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

    public ApiException(HttpStatusCode statusCode, ApiError error)
        : base(error.Message)
    {
        StatusCode = statusCode;
        ErrorCode = error.Code;
    }

    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsConflict => StatusCode == HttpStatusCode.Conflict;
    public bool IsValidationError => ErrorCode == "VALIDATION_FAILED";
    public bool IsInvalidOperation => ErrorCode == "INVALID_OPERATION";
}