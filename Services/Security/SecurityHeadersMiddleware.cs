namespace DailyPilot.Services.Security;

/// <summary>
/// Adds standard hardening response headers (PRD §9 security). The CSP allows
/// the jsDelivr CDN used for Bootstrap Icons and Chart.js, plus inline scripts/styles
/// the views rely on. In Development the connect-src is relaxed so Visual Studio
/// Browser Link / hot-reload (which talk to localhost over ws/http) aren't blocked.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // In Development, allow Browser Link / hot-reload websocket + localhost connections.
        var connectSrc = _isDevelopment
            ? "connect-src 'self' ws: wss: http://localhost:* https://localhost:*; "
            : "connect-src 'self'; ";

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        // geolocation=(self): the Route Planner needs the browser's location prompt;
        // () would auto-deny it site-wide without ever prompting.
        headers["Permissions-Policy"] = "geolocation=(self), microphone=(), camera=()";
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "img-src 'self' data:; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
            connectSrc +
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
