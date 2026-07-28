using DailyPilot.Data;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services.Api;

public interface IFcmPushSender
{
    bool IsEnabled { get; }
    Task SendToUserAsync(string userId, string title, string body, string? url = null);
}

/// <summary>
/// Sends native FCM notifications to a user's registered mobile devices. Disabled
/// gracefully (no-op) when Fcm:CredentialPath is unset or the key file is missing,
/// so environments without Firebase keep working. Invalid/unregistered tokens are
/// pruned on send.
/// </summary>
public class FcmPushSender : IFcmPushSender
{
    private const string Channel = "reminders";

    private static readonly object InitLock = new();
    private static bool _initAttempted;
    private static FirebaseMessaging? _messaging;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(ApplicationDbContext db, IConfiguration config, ILogger<FcmPushSender> logger)
    {
        _db = db;
        _logger = logger;
        EnsureInitialized(config, logger);
    }

    private static void EnsureInitialized(IConfiguration config, ILogger logger)
    {
        if (_initAttempted) return;
        lock (InitLock)
        {
            if (_initAttempted) return;
            _initAttempted = true;

            var path = config["Fcm:CredentialPath"];
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            {
                // Relative paths can miss when the process CWD isn't the content root (IIS).
                var fromBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
                if (File.Exists(fromBase)) path = fromBase;
            }
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                logger.LogInformation("FCM disabled: credential file not configured/found ({Path}).", path ?? "<null>");
                return;
            }
            try
            {
                var app = FirebaseApp.DefaultInstance
                          ?? FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(path) });
                _messaging = FirebaseMessaging.GetMessaging(app);
                logger.LogInformation("FCM initialized from {Path}.", path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FCM initialization failed — native push disabled.");
            }
        }
    }

    public bool IsEnabled => _messaging is not null;

    public async Task SendToUserAsync(string userId, string title, string body, string? url = null)
    {
        if (_messaging is null) return;

        var tokens = await _db.DeviceTokens.Where(t => t.UserId == userId).ToListAsync();
        if (tokens.Count == 0) return;

        var pruned = false;
        foreach (var device in tokens)
        {
            var message = new Message
            {
                Token = device.Token,
                Notification = new Notification { Title = title, Body = body },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification { ChannelId = Channel },
                },
                Data = url is null ? null : new Dictionary<string, string> { ["url"] = url },
            };

            try
            {
                await _messaging.SendAsync(message);
            }
            catch (FirebaseMessagingException ex) when (
                ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
            {
                _db.DeviceTokens.Remove(device);
                pruned = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM send to device {Id} failed.", device.Id);
            }
        }
        if (pruned) await _db.SaveChangesAsync();
    }
}
