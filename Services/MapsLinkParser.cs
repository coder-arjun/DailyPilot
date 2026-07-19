using System.Globalization;
using System.Text.RegularExpressions;

namespace DailyPilot.Services;

public readonly record struct GeoPoint(double Lat, double Lng);

/// <summary>
/// Extracts coordinates from pasted Google Maps URLs / raw "lat,lng" text.
/// Pure and static so it is unit-testable without the web host.
/// </summary>
public static class MapsLinkParser
{
    private const string Num = @"(-?\d+(?:\.\d+)?)";

    // !3d<lat>!4d<lng> — the pin itself; more precise than the @viewport centre.
    private static readonly Regex BangMarkers = new($@"!3d{Num}!4d{Num}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AtSegment = new($@"@{Num},{Num}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RawPair = new($@"^\s*{Num}\s*,\s*{Num}\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex QueryParam = new(@"[?&](?:q|query|destination)=([^&#]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlaceSegment = new(@"/place/([^/@?#]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryParse(string? input, out GeoPoint point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var text = input.Trim();

        var raw = RawPair.Match(text);
        if (raw.Success) return TryMakePoint(raw.Groups[1].Value, raw.Groups[2].Value, out point);

        var bang = BangMarkers.Match(text);
        if (bang.Success && TryMakePoint(bang.Groups[1].Value, bang.Groups[2].Value, out point)) return true;

        var query = QueryParam.Match(text);
        if (query.Success)
        {
            var decoded = Uri.UnescapeDataString(query.Groups[1].Value);
            var pair = RawPair.Match(decoded);
            if (pair.Success && TryMakePoint(pair.Groups[1].Value, pair.Groups[2].Value, out point)) return true;
        }

        var at = AtSegment.Match(text);
        if (at.Success && TryMakePoint(at.Groups[1].Value, at.Groups[2].Value, out point)) return true;

        return false;
    }

    public static bool IsShortLink(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)) return false;
        return uri.Host.ToLowerInvariant() switch
        {
            "maps.app.goo.gl" => true,
            "goo.gl" or "www.goo.gl" => uri.AbsolutePath.StartsWith("/maps", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    public static string? ExtractPlaceLabel(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = PlaceSegment.Match(url);
        if (!m.Success) return null;
        var label = Uri.UnescapeDataString(m.Groups[1].Value).Replace('+', ' ').Trim();
        return label.Length > 0 ? label : null;
    }

    /// <summary>
    /// Pulls the place-name query (and Google's feature id) out of a
    /// "google.com/maps?q=Name&amp;ftid=0x..:0x.." URL — the form the Maps app's
    /// Share → Copy link resolves to, which carries no coordinates at all.
    /// </summary>
    public static bool TryGetSearchQuery(string? url, out string query, out string? ftid)
    {
        query = string.Empty; ftid = null;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out _)) return false;
        var m = QRaw.Match(url);
        if (!m.Success) return false;
        query = Uri.UnescapeDataString(m.Groups[1].Value.Replace('+', ' ')).Trim();
        if (query.Length == 0) return false;
        var f = FtidRaw.Match(url);
        if (f.Success) ftid = Uri.UnescapeDataString(f.Groups[1].Value);
        return true;
    }

    /// <summary>
    /// Finds the first in-range "[lat,lng]" float pair in the output=embed page —
    /// out-of-range pairs (zoom radii, spans) are skipped.
    /// </summary>
    public static bool TryExtractEmbedPoint(string? html, out GeoPoint point)
    {
        point = default;
        if (string.IsNullOrEmpty(html)) return false;
        foreach (Match m in PairArray.Matches(html))
            if (TryMakePoint(m.Groups[1].Value, m.Groups[2].Value, out point)) return true;
        return false;
    }

    /// <summary>EU consent interstitial carries the real target in ?continue=.</summary>
    public static string? ExtractConsentContinue(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return null;
        if (!uri.Host.Equals("consent.google.com", StringComparison.OrdinalIgnoreCase)) return null;
        var m = ContinueRaw.Match(url);
        return m.Success ? Uri.UnescapeDataString(m.Groups[1].Value) : null;
    }

    /// <summary>
    /// Ordered geocoding attempts for a place-name query: the full string, then
    /// "name + last segment" (usually city/postal), then the name alone. Google's
    /// embed geocoder handles simple forms better than full comma-separated addresses.
    /// </summary>
    public static IEnumerable<string> QueryVariants(string query)
    {
        var seen = new HashSet<string>();
        var full = query.Trim();
        if (full.Length > 0 && seen.Add(full)) yield return full;

        var parts = full.Split(',', '،');
        var segments = new List<string>();
        foreach (var p in parts)
        {
            var t = p.Trim();
            if (t.Length > 0) segments.Add(t);
        }
        if (segments.Count < 2) yield break;

        var firstPlusLast = segments[0] + " " + segments[^1];
        if (seen.Add(firstPlusLast)) yield return firstPlusLast;
        if (seen.Add(segments[0])) yield return segments[0];
    }

    /// <summary>Place name = the part of the search query before the first comma (Latin or Arabic).</summary>
    public static string LabelFromQuery(string query)
    {
        var cut = query.Split(',', '،')[0].Trim();
        return cut.Length > 0 ? cut : query.Trim();
    }

    private static readonly Regex QRaw = new(@"[?&]q=([^&#]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FtidRaw = new(@"[?&]ftid=([^&#]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ContinueRaw = new(@"[?&]continue=([^&#]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PairArray = new($@"\[{Num}\s*,\s*{Num}\]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Great-circle distance in kilometres (mean Earth radius 6371.0088 km).</summary>
    public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0088;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    private static bool TryMakePoint(string latText, string lngText, out GeoPoint point)
    {
        point = default;
        if (!double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return false;
        if (!double.TryParse(lngText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)) return false;
        if (lat is < -90 or > 90 || lng is < -180 or > 180) return false;
        point = new GeoPoint(lat, lng);
        return true;
    }
}
