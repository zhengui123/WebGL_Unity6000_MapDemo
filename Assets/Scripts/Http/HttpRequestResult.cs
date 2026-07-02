/// <summary>
/// HTTP 请求结果（原始响应体与错误信息）。
/// </summary>
public class HttpRequestResult
{
    public bool IsSuccess { get; set; }
    public bool IsCancelled { get; set; }
    public long StatusCode { get; set; }
    public string RawBody { get; set; }
    public string Error { get; set; }

    public static HttpRequestResult Success(long statusCode, string rawBody)
    {
        return new HttpRequestResult
        {
            IsSuccess = true,
            StatusCode = statusCode,
            RawBody = rawBody ?? string.Empty,
        };
    }

    public static HttpRequestResult Failure(string error, long statusCode = 0, string rawBody = null)
    {
        return new HttpRequestResult
        {
            IsSuccess = false,
            StatusCode = statusCode,
            RawBody = rawBody ?? string.Empty,
            Error = error ?? "未知错误",
        };
    }

    public static HttpRequestResult Cancelled(string message = "请求已取消")
    {
        return new HttpRequestResult
        {
            IsSuccess = false,
            IsCancelled = true,
            Error = message,
        };
    }
}
