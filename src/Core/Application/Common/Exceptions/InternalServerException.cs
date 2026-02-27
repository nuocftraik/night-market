using System.Net;

namespace NightMarket.WebApi.Application.Common.Exceptions;

/// <summary>
/// Exception cho internal server errors hoặc unexpected errors
/// HTTP Status Code: 500 Internal Server Error
/// </summary>
public class InternalServerException : CustomException
{
    /// <summary>
    /// Constructor với message và optional error list
    /// </summary>
    /// <param name="message">Error message chính</param>
    /// <param name="errors">Danh sách detailed errors (optional)</param>
    public InternalServerException(string message, List<string>? errors = default)
        : base(message, errors, HttpStatusCode.InternalServerError)
    {
    }
}
