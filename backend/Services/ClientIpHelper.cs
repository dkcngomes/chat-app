using System.Net;

namespace backend.Services;

/// <summary>
/// Extracts the real client IP from a request, respecting reverse proxy headers.
/// Works for both Controller and Hub contexts.
/// </summary>
public static class ClientIpHelper
{
    /// <summary>
    /// Get client IP from an HTTP context.
    /// Prefers X-Forwarded-For (for reverse proxy / HF Spaces), falls back to RemoteIpAddress.
    /// </summary>
    public static string GetIpAddress(HttpContext httpContext)
    {
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            // X-Forwarded-For can be comma-separated: client, proxy1, proxy2
            var ip = forwarded.Split(',')[0].Trim();
            if (IPAddress.TryParse(ip, out _))
                return ip;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
