using DailyPilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers;

[Authorize]
public class RoutePlannerController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RoutePlannerController> _logger;

    public RoutePlannerController(IHttpClientFactory httpClientFactory, ILogger<RoutePlannerController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public IActionResult Index() => View();

    public sealed record ResolveRequest(string? Text);

    /// <summary>
    /// Turns pasted text (full Google Maps URL, maps.app.goo.gl short link, or raw
    /// "lat,lng") into coordinates. Short links hide the coordinates behind a
    /// redirect the browser can't read cross-origin, so we follow it here instead.
    /// Only known Google short-link hosts are ever fetched (no open proxying).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve([FromBody] ResolveRequest? request, CancellationToken ct)
    {
        var text = request?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return Json(new { ok = false, error = "Paste a Google Maps link or \"lat,lng\" coordinates." });

        if (MapsLinkParser.TryParse(text, out var point))
            return Json(new { ok = true, lat = point.Lat, lng = point.Lng, label = MapsLinkParser.ExtractPlaceLabel(text) });

        if (MapsLinkParser.IsShortLink(text))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("maps-resolver");
                using var response = await client.GetAsync(text, HttpCompletionOption.ResponseHeadersRead, ct);
                var finalUrl = response.RequestMessage?.RequestUri?.ToString();
                if (finalUrl is not null && MapsLinkParser.TryParse(finalUrl, out var resolved))
                    return Json(new { ok = true, lat = resolved.Lat, lng = resolved.Lng, label = MapsLinkParser.ExtractPlaceLabel(finalUrl) });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Short-link expansion failed");
            }
            return Json(new { ok = false, error = "Couldn't expand that short link. Open it in Google Maps and copy the full URL from the address bar, or paste \"lat,lng\"." });
        }

        return Json(new { ok = false, error = "Couldn't read coordinates from that. Paste a full Google Maps URL, a maps.app.goo.gl link, or \"lat,lng\"." });
    }
}
