using FluentValidation;
using MediatR;

namespace NightMarket.WebApi.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior để tự động validate requests
/// Chạy TRƯỚC handler, throw ValidationException nếu có lỗi
/// </summary>
/// <typeparam name="TRequest">Request type (IRequest&lt;TResponse&gt;)</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Constructor - inject tất cả validators cho TRequest
    /// </summary>
    /// <param name="validators">Danh sách validators (có thể empty)</param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Handle method - được gọi bởi MediatR pipeline
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // 1. Nếu không có validators, skip validation
        if (!_validators.Any())
        {
            return await next();
        }

        // 2. Tạo validation context
        var context = new ValidationContext<TRequest>(request);

        // 3. Chạy tất cả validators song song
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // 4. Lấy tất cả validation failures
        var failures = validationResults
            .Where(r => r.Errors.Any())
            .SelectMany(r => r.Errors)
            .ToList();

        // 5. Nếu có lỗi, throw ValidationException
        if (failures.Any())
        {
            throw new ValidationException(failures);
        }

        // 6. Validation passed - tiếp tục vào handler
        return await next();
    }
}
