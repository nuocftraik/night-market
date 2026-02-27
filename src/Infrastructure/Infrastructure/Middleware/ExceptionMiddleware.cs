using NightMarket.WebApi.Application.Common.Exceptions;
using NightMarket.WebApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Context;
using System.Net;

namespace NightMarket.WebApi.Infrastructure.Middleware;

/// <summary>
/// Middleware để catch và handle tất cả exceptions
/// Phải đặt đầu tiên trong middleware pipeline
/// </summary>
internal class ExceptionMiddleware : IMiddleware
{
    private readonly ICurrentUser _currentUser;
    private readonly ISerializerService _jsonSerializer;

    public ExceptionMiddleware(
        ICurrentUser currentUser,
        ISerializerService jsonSerializer)
    {
        _currentUser = currentUser;
        _jsonSerializer = jsonSerializer;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            // Continue với request pipeline
            await next(context);
        }
        catch (Exception exception)
        {
            // 1. Lấy user context
            string email = _currentUser.GetUserEmail() is string userEmail && !string.IsNullOrEmpty(userEmail) ? userEmail : "Anonymous";
            var userId = _currentUser.GetUserId();

            // 2. Push context vào Serilog
            if (userId != Guid.Empty)
                LogContext.PushProperty("UserId", userId);
            LogContext.PushProperty("UserEmail", email);

            // 3. Generate unique error ID
            string errorId = Guid.NewGuid().ToString();
            LogContext.PushProperty("ErrorId", errorId);
            LogContext.PushProperty("StackTrace", exception.StackTrace);

            // 4. Tạo ErrorResult
            var errorResult = new ErrorResult
            {
                Source = exception.TargetSite?.DeclaringType?.FullName,
                Exception = exception.Message.Trim(),
                ErrorId = errorId,
                SupportMessage = $"Provide the ErrorId {errorId} to the support team for further analysis."
            };

            // 5. Handle inner exception (unwrap)
            if (exception is not CustomException && exception.InnerException != null)
            {
                while (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                }
            }

            // 6. Handle FluentValidation exceptions (sẽ hoạt động sau khi add FluentValidation ở Step 14)
            if (exception.GetType().FullName == "FluentValidation.ValidationException")
            {
                errorResult.Exception = "One or More Validations failed.";
                // We will handle FluentValidation natively once it is installed.
                // Using dynamic/reflection or just checking Name here avoids hard dependency if not yet installed.
                dynamic fluentException = exception;
                if (fluentException.Errors != null)
                {
                    foreach (var error in fluentException.Errors)
                    {
                        errorResult.Messages.Add(error.ErrorMessage);
                    }
                }
            }

            // 7. Set status code dựa trên exception type
            switch (exception)
            {
                case CustomException e:
                    errorResult.StatusCode = (int)e.StatusCode;
                    if (e.ErrorMessages is not null)
                    {
                        errorResult.Messages = e.ErrorMessages;
                    }
                    break;

                case KeyNotFoundException:
                    errorResult.StatusCode = (int)HttpStatusCode.NotFound;
                    break;

                default:
                    if (exception.GetType().FullName == "FluentValidation.ValidationException")
                    {
                        errorResult.StatusCode = (int)HttpStatusCode.UnprocessableEntity; // Often 422 for validation
                    }
                    else
                    {
                        errorResult.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                    break;
            }

            // 8. Log error
            Log.Error($"{errorResult.Exception} Request failed with Status Code {errorResult.StatusCode} and Error Id {errorId}.");

            // 9. Write error response
            var response = context.Response;
            if (!response.HasStarted)
            {
                response.ContentType = "application/json";
                response.StatusCode = errorResult.StatusCode;
                await response.WriteAsync(_jsonSerializer.Serialize(errorResult));
            }
            else
            {
                Log.Warning("Can't write error response. Response has already started.");
            }
        }
    }
}
