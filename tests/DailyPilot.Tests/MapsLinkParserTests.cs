using DailyPilot.Services;

namespace DailyPilot.Tests;

public class MapsLinkParserTests
{
    // ---------- Raw coordinate text ----------

    [Theory]
    [InlineData("12.9716, 77.5946", 12.9716, 77.5946)]
    [InlineData("12.9716,77.5946", 12.9716, 77.5946)]
    [InlineData("-33.8688,151.2093", -33.8688, 151.2093)]
    [InlineData("  48.8566 , 2.3522  ", 48.8566, 2.3522)]
    public void TryParse_RawLatLng_Parses(string input, double lat, double lng)
    {
        Assert.True(MapsLinkParser.TryParse(input, out var p));
        Assert.Equal(lat, p.Lat, 6);
        Assert.Equal(lng, p.Lng, 6);
    }

    // ---------- Full Google Maps URLs ----------

    [Fact]
    public void TryParse_AtSegmentUrl_Parses()
    {
        var url = "https://www.google.com/maps/@12.9716,77.5946,15z";
        Assert.True(MapsLinkParser.TryParse(url, out var p));
        Assert.Equal(12.9716, p.Lat, 6);
        Assert.Equal(77.5946, p.Lng, 6);
    }

    [Fact]
    public void TryParse_PlaceUrlWithBangMarkers_Parses()
    {
        var url = "https://www.google.com/maps/place/Some+Place/data=!4m6!3m5!1s0x0:0x0!8m2!3d13.1986348!4d77.7065928!16s";
        Assert.True(MapsLinkParser.TryParse(url, out var p));
        Assert.Equal(13.1986348, p.Lat, 6);
        Assert.Equal(77.7065928, p.Lng, 6);
    }

    [Fact]
    public void TryParse_UrlWithBothAtAndBangMarkers_PrefersBangMarkers()
    {
        // @ is the viewport centre; !3d/!4d is the actual pin — the pin must win.
        var url = "https://www.google.com/maps/place/Some+Place/@13.20,77.70,17z/data=!3m1!4b1!4m6!3m5!1s0x0:0x0!8m2!3d13.1986348!4d77.7065928";
        Assert.True(MapsLinkParser.TryParse(url, out var p));
        Assert.Equal(13.1986348, p.Lat, 6);
        Assert.Equal(77.7065928, p.Lng, 6);
    }

    [Theory]
    [InlineData("https://maps.google.com/?q=12.9716,77.5946")]
    [InlineData("https://www.google.com/maps/search/?api=1&query=12.9716,77.5946")]
    public void TryParse_QueryParamUrls_Parse(string url)
    {
        Assert.True(MapsLinkParser.TryParse(url, out var p));
        Assert.Equal(12.9716, p.Lat, 6);
        Assert.Equal(77.5946, p.Lng, 6);
    }

    [Fact]
    public void TryParse_NegativeCoordinatesInUrl_Parse()
    {
        var url = "https://www.google.com/maps/place/Sydney/@-33.8688,151.2093,12z";
        Assert.True(MapsLinkParser.TryParse(url, out var p));
        Assert.Equal(-33.8688, p.Lat, 6);
        Assert.Equal(151.2093, p.Lng, 6);
    }

    // ---------- Rejection ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("95,77")]              // lat out of range
    [InlineData("12,190")]             // lng out of range
    [InlineData("https://example.com/nothing-here")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        Assert.False(MapsLinkParser.TryParse(input, out _));
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        Assert.False(MapsLinkParser.TryParse(null, out _));
    }

    // ---------- Short links ----------

    [Theory]
    [InlineData("https://maps.app.goo.gl/AbCdEf123", true)]
    [InlineData("https://goo.gl/maps/AbCdEf123", true)]
    [InlineData("https://www.google.com/maps/@12.97,77.59,15z", false)]
    [InlineData("12.97,77.59", false)]
    public void IsShortLink_DetectsGoogleShortHosts(string input, bool expected)
    {
        Assert.Equal(expected, MapsLinkParser.IsShortLink(input));
    }

    // ---------- Place label ----------

    [Fact]
    public void ExtractPlaceLabel_FromPlaceUrl_ReturnsDecodedName()
    {
        var url = "https://www.google.com/maps/place/Kempegowda+International+Airport/@13.1986,77.7066,17z/data=!3m1";
        Assert.Equal("Kempegowda International Airport", MapsLinkParser.ExtractPlaceLabel(url));
    }

    [Fact]
    public void ExtractPlaceLabel_PercentEncoded_ReturnsDecodedName()
    {
        var url = "https://www.google.com/maps/place/Caf%C3%A9+de+Flore/@48.854,2.3325,17z";
        Assert.Equal("Café de Flore", MapsLinkParser.ExtractPlaceLabel(url));
    }

    [Fact]
    public void ExtractPlaceLabel_NoPlaceSegment_ReturnsNull()
    {
        Assert.Null(MapsLinkParser.ExtractPlaceLabel("https://www.google.com/maps/@12.97,77.59,15z"));
        Assert.Null(MapsLinkParser.ExtractPlaceLabel("12.97,77.59"));
    }

    // ---------- Name-search URLs (Share → Copy link from the Maps app) ----------

    [Fact]
    public void TryGetSearchQuery_NameAndFtid_ExtractsBoth()
    {
        var url = "https://www.google.com/maps?q=Twigs+Beauty+Lounge%D8%8C+%D8%B9%D9%85%D9%91%D8%A7%D9%86+11821&ftid=0x151ca14f118fcb2f:0xa6158e4e8b82fa6c&entry=gps";
        Assert.True(MapsLinkParser.TryGetSearchQuery(url, out var q, out var ftid));
        Assert.StartsWith("Twigs Beauty Lounge", q);
        Assert.Equal("0x151ca14f118fcb2f:0xa6158e4e8b82fa6c", ftid);
    }

    [Fact]
    public void TryGetSearchQuery_NoQParam_ReturnsFalse()
    {
        Assert.False(MapsLinkParser.TryGetSearchQuery("https://www.google.com/maps/@12.97,77.59,15z", out _, out _));
        Assert.False(MapsLinkParser.TryGetSearchQuery("not a url", out _, out _));
    }

    // ---------- Embed-page coordinate extraction ----------

    [Fact]
    public void TryExtractEmbedPoint_FirstValidPair_Extracts()
    {
        var html = "<script>window.APP_OPTIONS=[1,null,[31.9771854,35.8549766],\"x\"]</script>";
        Assert.True(MapsLinkParser.TryExtractEmbedPoint(html, out var p));
        Assert.Equal(31.9771854, p.Lat, 6);
        Assert.Equal(35.8549766, p.Lng, 6);
    }

    [Fact]
    public void TryExtractEmbedPoint_SkipsOutOfRangePairs()
    {
        // APP_INITIALIZATION_STATE-style junk (zoom radius, lng, lat) precedes the real pair.
        var html = "=[[[251478.57316820518,76.3133952],[31.9771854,35.8549766]]]";
        Assert.True(MapsLinkParser.TryExtractEmbedPoint(html, out var p));
        Assert.Equal(31.9771854, p.Lat, 6);
        Assert.Equal(35.8549766, p.Lng, 6);
    }

    [Fact]
    public void TryExtractEmbedPoint_NoPair_ReturnsFalse()
    {
        Assert.False(MapsLinkParser.TryExtractEmbedPoint("<html>no coordinates here</html>", out _));
        Assert.False(MapsLinkParser.TryExtractEmbedPoint(null, out _));
    }

    // ---------- Consent interstitial (EU servers) ----------

    [Fact]
    public void ExtractConsentContinue_ReturnsInnerUrl()
    {
        var url = "https://consent.google.com/m?continue=https%3A%2F%2Fwww.google.com%2Fmaps%2Fplace%2FX%2F%4031.97%2C35.85%2C17z&gl=DE";
        Assert.Equal("https://www.google.com/maps/place/X/@31.97,35.85,17z", MapsLinkParser.ExtractConsentContinue(url));
    }

    [Fact]
    public void ExtractConsentContinue_NotConsentHost_ReturnsNull()
    {
        Assert.Null(MapsLinkParser.ExtractConsentContinue("https://www.google.com/maps?continue=x"));
    }

    // ---------- Query variants for embed geocoding ----------

    [Fact]
    public void QueryVariants_FullAddress_YieldsFullThenFirstPlusLastThenFirst()
    {
        var q = "Twigs Beauty Lounge، ش. إمثاري النعيمات، عمّان 11821";
        var v = MapsLinkParser.QueryVariants(q).ToArray();
        Assert.Equal(new[]
        {
            q,
            "Twigs Beauty Lounge عمّان 11821",
            "Twigs Beauty Lounge",
        }, v);
    }

    [Fact]
    public void QueryVariants_NoCommas_YieldsSingleVariant()
    {
        Assert.Equal(new[] { "Plain Name" }, MapsLinkParser.QueryVariants("Plain Name").ToArray());
    }

    [Fact]
    public void QueryVariants_TwoSegments_NoDuplicates()
    {
        // first+last == "A B" and first == "A": all three distinct, but a
        // degenerate "A, A" must not produce duplicate entries.
        var v = MapsLinkParser.QueryVariants("A, A").ToArray();
        Assert.Equal(new[] { "A, A", "A A", "A" }, v);
    }

    // ---------- Label from a search query ----------

    [Theory]
    [InlineData("Twigs Beauty Lounge، ش. إمثاري النعيمات، عمّان 11821", "Twigs Beauty Lounge")]
    [InlineData("Blue Tokai Coffee, Indiranagar, Bengaluru", "Blue Tokai Coffee")]
    [InlineData("Plain Name", "Plain Name")]
    public void LabelFromQuery_TakesNameBeforeFirstComma(string q, string expected)
    {
        Assert.Equal(expected, MapsLinkParser.LabelFromQuery(q));
    }

    // ---------- Haversine ----------

    [Fact]
    public void HaversineKm_LondonToParis_IsAbout343Km()
    {
        var d = MapsLinkParser.HaversineKm(51.5007, -0.1246, 48.8566, 2.3522);
        Assert.InRange(d, 340, 347);
    }

    [Fact]
    public void HaversineKm_SamePoint_IsZero()
    {
        Assert.Equal(0, MapsLinkParser.HaversineKm(12.9716, 77.5946, 12.9716, 77.5946), 6);
    }
}
