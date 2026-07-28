import * as Notifications from 'expo-notifications';
import * as Speech from 'expo-speech';
import * as TaskManager from 'expo-task-manager';

/**
 * Killed/locked-state reminder handling. The server sends DATA-ONLY FCM messages
 * (title/body/url/speak/channelId) so this task — not the OS — renders the
 * notification, which is what lets us also read it aloud with the screen locked.
 * Runs headless when the app is killed; module scope, imported from the root
 * layout so it is registered before anything else.
 */
export const BACKGROUND_NOTIFICATION_TASK = 'daypilot-remote-reminder';

/** Recently-handled reminders (title|body per minute) so the foreground listener
 *  doesn't speak the same reminder a second time after we display it here. */
const recentlyHandled = new Set<string>();

export function markHandled(key: string): void {
  recentlyHandled.add(key);
  setTimeout(() => recentlyHandled.delete(key), 90_000);
}

export function wasHandled(key: string): boolean {
  return recentlyHandled.has(key);
}

/** Speak and resolve only when done (or after a hard cap, staying inside the
 *  ~30s Android headless-task budget). */
function speakAndWait(text: string, timeoutMs = 25_000): Promise<void> {
  return new Promise((resolve) => {
    const timer = setTimeout(() => {
      try {
        Speech.stop();
      } catch {
        // engine may already be gone
      }
      resolve();
    }, timeoutMs);
    const finish = () => {
      clearTimeout(timer);
      resolve();
    };
    Speech.speak(text, { language: 'en', onDone: finish, onStopped: finish, onError: finish });
  });
}

type RemoteData = Record<string, unknown>;

/** The FCM data payload can arrive at different nesting depths per platform/state. */
function extractData(raw: unknown): RemoteData {
  const d = raw as RemoteData | null | undefined;
  if (!d) return {};
  if (typeof d.title === 'string' || typeof d.body === 'string') return d;
  const inner = (d.data ?? (d.notification as RemoteData | undefined)?.data) as RemoteData | undefined;
  if (inner && (typeof inner.title === 'string' || typeof inner.body === 'string')) return inner;
  return d;
}

TaskManager.defineTask(BACKGROUND_NOTIFICATION_TASK, async ({ data, error }) => {
  if (error) return;
  try {
    const payload = extractData(data);
    const title = typeof payload.title === 'string' ? payload.title : '';
    const body = typeof payload.body === 'string' ? payload.body : '';
    if (!title && !body) return;

    const url = typeof payload.url === 'string' ? payload.url : '';
    const channelId = typeof payload.channelId === 'string' && payload.channelId ? payload.channelId : 'reminders';
    markHandled(`${title}|${body}`);

    await Notifications.scheduleNotificationAsync({
      content: {
        title: title || 'DayPilot',
        body,
        sound: true,
        data: { url },
      },
      trigger: { channelId },
    });

    if (payload.speak === '1') {
      const text = [title, body].filter(Boolean).join('. ');
      // MUST await: in a headless (killed/locked) context the JS runtime is torn
      // down as soon as this task resolves — fire-and-forget speech never gets to
      // make a sound because the TTS engine is still initializing.
      if (text) await speakAndWait(text);
    }
  } catch {
    // Never throw from a headless task.
  }
});

try {
  Notifications.registerTaskAsync(BACKGROUND_NOTIFICATION_TASK);
} catch {
  // Registration can fail in environments without Play services (e.g. some emulators).
}
