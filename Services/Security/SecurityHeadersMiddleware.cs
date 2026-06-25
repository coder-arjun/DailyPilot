namespace DailyPilot.Services.Security;

/// <summary>
/// Adds standard hardening response headers (PRD §9 security). The CSP allows
/// the jsDelivr CDN used for Bootstrap Icons and Chart.js, plus inline scripts/styles
/// the views rely on. Tightening script-src to nonces is a future enhancement.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "img-src 'self' data:; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
