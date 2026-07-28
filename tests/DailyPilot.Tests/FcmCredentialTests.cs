using Google.Apis.Auth.OAuth2;

namespace DailyPilot.Tests;

public class FcmCredentialTests
{
    /// <summary>
    /// When the developer's Firebase service-account key is present, prove it parses
    /// as a valid Google credential (catches a corrupted/wrong-file download early).
    /// Environments without the key skip silently.
    /// </summary>
    [Fact]
    public void FirebaseServiceAccountKey_WhenPresent_ParsesAsCredential()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "secrets", "firebase-service-account.json"));
        if (!File.Exists(path)) return;

        var credential = GoogleCredential.FromFile(path);
        Assert.NotNull(credential);
        Assert.True(credential.UnderlyingCredential is ServiceAccountCredential,
            "Expected a service-account key (Project settings → Service accounts → Generate new private key).");
    }
}
