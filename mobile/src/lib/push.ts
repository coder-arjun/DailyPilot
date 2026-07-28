import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import * as Speech from 'expo-speech';
import { Platform } from 'react-native';
import { api } from '@/lib/api/client';
import { wasHandled } from '@/lib/push-background';
import { queryClient } from '@/lib/queryClient';

/** Foreground presentation: reminders should still show as a banner. */
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: true,
    shouldSetBadge: false,
  }),
});

/** Server sends web-style urls in data.url — map them onto app routes. */
const urlToRoute: Record<string, string> = {
  '/Tasks': '/(app)',
  '/Habits': '/(app)/habits',
};

export function routeForNotification(data: Record<string, unknown> | null | undefined): string {
  const url = typeof data?.url === 'string' ? data.url : '';
  return urlToRoute[url] ?? '/(app)';
}

/**
 * Ask permission (Android 13+ POST_NOTIFICATIONS), ensure the "reminders"
 * channel exists, fetch the native FCM token, and register it with the server.
 * Safe to call on every sign-in; no-ops on emulators without Play services
 * and when permission is denied.
 */
export async function registerForPushAsync(): Promise<void> {
  if (!Device.isDevice) return;

  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('reminders', {
      name: 'Reminders',
      importance: Notifications.AndroidImportance.MAX,
      vibrationPattern: [0, 250, 250, 250],
      lightColor: '#6366F1',
      // Full content on the lock screen — reminders must be visible when locked.
      lockscreenVisibility: Notifications.AndroidNotificationVisibility.PUBLIC,
      bypassDnd: false,
    });
  }

  const settings = await Notifications.getPermissionsAsync();
  const granted =
    settings.granted || (await Notifications.requestPermissionsAsync()).granted;
  if (!granted) return;

  const token = (await Notifications.getDevicePushTokenAsync()).data as string;
  await api.post('/devices', { token, platform: Platform.OS });
}

/** Best-effort: remove this device's token server-side (call BEFORE logout clears auth). */
export async function unregisterPushAsync(): Promise<void> {
  try {
    if (!Device.isDevice) return;
    const token = (await Notifications.getDevicePushTokenAsync()).data as string;
    await api.delete('/devices', { data: { token } });
  } catch {
    // token cleanup is best-effort; server also prunes dead tokens on send
  }
}

type AccountShape = { settings?: { speakReminders?: boolean } } | undefined;

async function shouldSpeak(): Promise<boolean> {
  const cached = queryClient.getQueryData<AccountShape>(['account']);
  if (cached?.settings?.speakReminders !== undefined) return !!cached.settings.speakReminders;
  try {
    const { data } = await api.get<AccountShape>('/account');
    return !!data?.settings?.speakReminders;
  } catch {
    return false;
  }
}

/**
 * Web parity for "speak reminders": while the app is open, read the reminder
 * aloud when it arrives (the web does the same only while a tab is open —
 * when the app is killed, the system shows the notification silently spoken-wise).
 */
export function watchSpokenReminders(): () => void {
  const sub = Notifications.addNotificationReceivedListener(async (notification) => {
    const { title, body } = notification.request.content;
    // The background task already displayed+spoke this one (data-only path).
    if (wasHandled(`${title ?? ''}|${body ?? ''}`)) return;
    if (!(await shouldSpeak())) return;
    const text = [title, body].filter(Boolean).join('. ');
    if (text) Speech.speak(text, { language: 'en' });
  });
  return () => sub.remove();
}

/** Keep the server current when FCM rotates the token. */
export function watchPushTokenRotation(): () => void {
  const sub = Notifications.addPushTokenListener((t) => {
    const token = typeof t.data === 'string' ? t.data : null;
    if (token) api.post('/devices', { token, platform: Platform.OS }).catch(() => {});
  });
  return () => sub.remove();
}

/**
 * Navigation for notification taps: fires `navigate` for taps while running,
 * and returns the cold-start route (app opened by tapping a notification) if any.
 */
export async function wireNotificationTaps(navigate: (route: string) => void): Promise<() => void> {
  const initial = await Notifications.getLastNotificationResponseAsync();
  if (initial) {
    navigate(routeForNotification(initial.notification.request.content.data));
  }
  const sub = Notifications.addNotificationResponseReceivedListener((response) => {
    navigate(routeForNotification(response.notification.request.content.data));
  });
  return () => sub.remove();
}
