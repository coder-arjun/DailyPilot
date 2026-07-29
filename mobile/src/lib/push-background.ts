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

/** TTS engines can stall on pictographs (⏰/🔔 reminder prefixes) — speak clean text. */
export function sanitizeForSpeech(text: string): string {
  return text
    .replace(/[\p{Extended_Pictographic}\u{FE0F}\u{200D}]/gu, '')
    .replace(/\s+/g, ' ')
    .trim();
}

/** One speak attempt. Resolves 'done' | 'never-started' | 'timeout'. */
function trySpeak(text: string, capMs: number): Promise<'done' | 'never-started' | 'timeout'> {
  return new Promise((resolve) => {
    let started = false;
    let settled = false;
    const settle = (result: 'done' | 'never-started' | 'timeout') => {
      if (settled) return;
      settled = true;
      clearTimeout(startTimer);
      clearTimeout(capTimer);
      resolve(result);
    };
    // If the engine never even begins within 4s, it is wedged or unavailable.
    const startTimer = setTimeout(() => {
      if (!started) settle('never-started');
    }, 4_000);
    const capTimer = setTimeout(() => settle('timeout'), capMs);
    Speech.speak(text, {
      language: 'en',
      onStart: () => {
        started = true;
      },
      onDone: () => settle('done'),
      onStopped: () => settle('done'),
      onError: () => settle(started ? 'done' : 'never-started'),
    });
  });
}

/**
 * Robust spoken reminder: clears any wedged engine state first, then speaks,
 * retrying once if the engine never starts. A previous stuck run must not be
 * able to permanently silence all future reminders. Stays inside the ~30s
 * Android headless-task budget.
 */
export async function speakAndWait(rawText: string): Promise<void> {
  const text = sanitizeForSpeech(rawText);
  if (!text) return;
  for (let attempt = 0; attempt < 2; attempt++) {
    try {
      Speech.stop(); // clear anything a prior run left wedged
    } catch {
      // engine not ready yet — fine
    }
    const result = await trySpeak(text, 20_000);
    if (result !== 'never-started') return; // spoke (or at least ran to cap)
    await new Promise((r) => setTimeout(r, 1_500)); // let the engine re-bind, then retry
  }
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
