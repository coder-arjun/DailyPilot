using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace DailyPilot.Tests;

public class FcmSendPipelineTests
{
    /// <summary>
    /// End-to-end probe of the FCM send path (credential auth + payload shape +
    /// FirebaseAdmin version behavior) using a deliberately invalid token. A healthy
    /// pipeline gets an Unregistered/InvalidArgument rejection FROM GOOGLE; auth or
    /// serialization failures surface as different exceptions and fail the test.
    /// Skips silently when the developer key file is absent.
    /// </summary>
    [Fact]
    public async Task DataOnlySend_WithFakeToken_IsRejectedAsInvalidToken()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "secrets", "firebase-service-account.json"));
        if (!File.Exists(path)) return;

        var app = FirebaseApp.DefaultInstance
                  ?? FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(path) });
        var messaging = FirebaseMessaging.GetMessaging(app);

        var message = new Message
        {
            Token = "fake-probe-token-000",
            Android = new AndroidConfig { Priority = Priority.High },
            Data = new Dictionary<string, string>
            {
                ["title"] = "probe", ["body"] = "probe", ["url"] = "", ["speak"] = "0", ["channelId"] = "reminders",
            },
        };

        var ex = await Assert.ThrowsAsync<FirebaseMessagingException>(() => messaging.SendAsync(message));
        Assert.True(
            ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument,
            $"Expected invalid-token rejection, got: {ex.MessagingErrorCode} / {ex.Message}");
    }
}
