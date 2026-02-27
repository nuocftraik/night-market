namespace NightMarket.WebApi.Infrastructure.Middleware;

/// <summary>
/// Model để trả về error response cho client
/// Format nhất quán cho tất cả errors
/// </summary>
public class ErrorResult
{
    /// <summary>
    /// Danh sách error messages (cho validation errors)
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Source của exception (class và method name)
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Exception message chính
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Unique error ID để tracking và debugging
    /// </summary>
    public string? ErrorId { get; set; }

    /// <summary>
    /// Support message hướng dẫn user liên hệ support team
    /// </summary>
    public string? SupportMessage { get; set; }

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }
}
