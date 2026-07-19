using DailyPilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers;

[Authorize]
public class RoutePlannerController : Controller
{
    private const int MaxEmbedBodyBytes = 262_144;

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
    /// redirect the browser can't read cross-origin, so we follow it here. The Maps
    /// app's Share → Copy link resolves to a name-only search URL with no
    /// coordinates at all — for those we ask the keyless output=embed endpoint,
    /// which returns the place's true position. Only Google hosts are ever fetched.
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

        var client = _httpClientFactory.CreateClient("maps-resolver");

        if (MapsLinkParser.IsShortLink(text))
        {
            try
            {
                using var response = await client.GetAsync(text, HttpCompletionOption.ResponseHeadersRead, ct);
                var finalUrl = response.RequestMessage?.RequestUri?.ToString();
                if (finalUrl is not null)
                {
                    finalUrl = MapsLinkParser.ExtractConsentContinue(finalUrl) ?? finalUrl;

                    if (MapsLinkParser.TryParse(finalUrl, out var resolved))
                        return Json(new { ok = true, lat = resolved.Lat, lng = resolved.Lng, label = MapsLinkParser.ExtractPlaceLabel(finalUrl) });

                    var fromEmbed = await ResolveViaEmbedAsync(client, finalUrl, ct);
                    if (fromEmbed is not null) return fromEmbed;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Short-link expansion failed");
            }
            return Json(new { ok = false, error = "Couldn't expand that short link. Open it in Google Maps and copy the full URL from the address bar, or paste \"lat,lng\"." });
        }

        // A pasted full Google URL whose q= is a place name rather than coordinates.
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Host.Contains("google", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var fromEmbed = await ResolveViaEmbedAsync(client, text, ct);
                if (fromEmbed is not null) return fromEmbed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Embed lookup failed");
            }
        }

        return Json(new { ok = false, error = "Couldn't read coordinates from that. Paste a full Google Maps URL, a maps.app.goo.gl link, or \"lat,lng\"." });
    }

    /// <summary>
    /// Looks the place up on the keyless output=embed endpoint. Full comma-separated
    /// addresses often fail its geocoder, so progressively simpler variants of the
    /// query are tried until one returns a coordinate pair.
    /// </summary>
    private async Task<IActionResult?> ResolveViaEmbedAsync(HttpClient client, string mapsUrl, CancellationToken ct)
    {
        if (!MapsLinkParser.TryGetSearchQuery(mapsUrl, out var q, out _)) return null;

        foreach (var variant in MapsLinkParser.QueryVariants(q))
        {
            var embedUrl = "https://www.google.com/maps?q=" + Uri.EscapeDataString(variant) + "&output=embed&hl=en";
            using var response = await client.GetAsync(embedUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) continue;

            var html = await ReadBoundedAsync(response, MaxEmbedBodyBytes, ct);
            if (MapsLinkParser.TryExtractEmbedPoint(html, out var point))
                return Json(new { ok = true, lat = point.Lat, lng = point.Lng, label = MapsLinkParser.LabelFromQuery(q) });
        }
        return null;
    }

    private static async Task<string> ReadBoundedAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[maxBytes];
        var total = 0;
        int read;
        while (total < maxBytes && (read = await stream.ReadAsync(buffer.AsMemory(total, maxBytes - total), ct)) > 0)
            total += read;
        return System.Text.Encoding.UTF8.GetString(buffer, 0, total);
    }
}
