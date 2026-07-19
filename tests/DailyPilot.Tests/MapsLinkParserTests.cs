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
