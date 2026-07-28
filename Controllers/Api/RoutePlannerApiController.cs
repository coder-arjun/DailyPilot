using DailyPilot.Services;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers.Api;

/// <summary>
/// Mobile JWT twin of <see cref="RoutePlannerController.Resolve"/>: turns pasted text (full
/// Google Maps URL, maps.app.goo.gl short link, or raw "lat,lng") into coordinates via the
/// same fallback chain (TryParse -> short-link expansion with consent unwrap -> embed
/// geocoding), the same <see cref="MapsLinkParser"/> helpers and the same named HttpClient
/// "maps-resolver". The ~90 lines below are duplicated rather than extracted into a shared
/// service, to guarantee zero risk of changing <see cref="RoutePlannerController"/>'s
/// behavior (see task-4-report.md).
/// </summary>
[Route("api/v1/routeplanner")]
public class RoutePlannerApiController : ApiControllerBase
{
    private const int MaxEmbedBodyBytes = 262_144;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RoutePlannerApiController> _logger;

    public RoutePlannerApiController(IHttpClientFactory httpClientFactory, ILogger<RoutePlannerApiController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public sealed record ResolveRequest(string? Text);

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve([FromBody] ResolveRequest? request, CancellationToken ct)
    {
        var text = request?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return ApiError(400, "Paste a Google Maps link or \"lat,lng\" coordinates.");

        if (MapsLinkParser.TryParse(text, out var point))
            return Ok(new { lat = point.Lat, lng = point.Lng, label = MapsLinkParser.ExtractPlaceLabel(text) });

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
                        return Ok(new { lat = resolved.Lat, lng = resolved.Lng, label = MapsLinkParser.ExtractPlaceLabel(finalUrl) });

                    var fromEmbed = await ResolveViaEmbedAsync(client, finalUrl, ct);
                    if (fromEmbed is not null) return fromEmbed;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Short-link expansion failed");
            }
            return ApiError(422, "Couldn't expand that short link. Open it in Google Maps and copy the full URL from the address bar, or paste \"lat,lng\".");
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

        return ApiError(422, "Couldn't read coordinates from that. Paste a full Google Maps URL, a maps.app.goo.gl link, or \"lat,lng\".");
    }

    /// <summary>
    /// Looks the place up on the keyless output=embed endpoint. Full comma-separated
    /// addresses often fail its geocoder, so progressively simpler variants of the
    /// query are tried until one returns a coordinate pair.
    /// </summary>
    private async Task<IActionResult?> ResolveViaEmbedAsync(HttpClient client, string mapsUrl, CancellationToken ct)
    {
        if (!MapsLinkParser.TryGetPlaceQuery(mapsUrl, out var q)) return null;

        foreach (var variant in MapsLinkParser.QueryVariants(q))
        {
            var embedUrl = "https://www.google.com/maps?q=" + Uri.EscapeDataString(variant) + "&output=embed&hl=en";
            using var response = await client.GetAsync(embedUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) continue;

            var html = await ReadBoundedAsync(response, MaxEmbedBodyBytes, ct);
            if (MapsLinkParser.TryExtractEmbedPoint(html, out var point))
                return Ok(new { lat = point.Lat, lng = point.Lng, label = MapsLinkParser.LabelFromQuery(q) });
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
