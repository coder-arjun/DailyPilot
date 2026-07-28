import { Ionicons } from '@expo/vector-icons';
import DateTimePicker from '@react-native-community/datetimepicker';
import { useRouter } from 'expo-router';
import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Linking,
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  View,
} from 'react-native';
import { Button, Card, ErrorText, Input, Label, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { config } from '@/lib/config';
import { theme } from '@/lib/theme';
import { useAccount, useUpdateSettings } from '@/features/account/api';
import { useAuthStore } from '@/stores/authStore';

const { colors, radius, spacing } = theme;

// The API base is ".../api/v1"; the web app lives at the origin above it.
const siteUrl = config.apiUrl.replace(/\/api\/v1\/?$/, '');

const toTimeString = (d: Date) =>
  `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;

export default function SettingsScreen() {
  const router = useRouter();
  const { data: account, isLoading, isError, error } = useAccount();
  const updateSettings = useUpdateSettings();

  const [displayName, setDisplayName] = useState('');
  const [morningOn, setMorningOn] = useState(true);
  const [morningTime, setMorningTime] = useState('08:00');
  const [eodOn, setEodOn] = useState(true);
  const [eodTime, setEodTime] = useState('20:00');
  const [carryForward, setCarryForward] = useState(true);
  const [speak, setSpeak] = useState(false);
  const [picker, setPicker] = useState<'morning' | 'eod' | null>(null);
  const [saveError, setSaveError] = useState('');
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (!account) return;
    setDisplayName(account.displayName ?? '');
    setMorningOn(account.settings.enableMorningReminder);
    setMorningTime(account.settings.morningReminderTime);
    setEodOn(account.settings.enableEndOfDayReminder);
    setEodTime(account.settings.endOfDayReminderTime);
    setCarryForward(account.settings.enableAutoCarryForward);
    setSpeak(account.settings.speakReminders);
  }, [account]);

  async function onSave() {
    setSaveError('');
    setSaved(false);
    try {
      await updateSettings.mutateAsync({
        displayName: displayName.trim(),
        enableMorningReminder: morningOn,
        morningReminderTime: morningTime,
        enableEndOfDayReminder: eodOn,
        endOfDayReminderTime: eodTime,
        enableAutoCarryForward: carryForward,
        speakReminders: speak,
      });
      setSaved(true);
    } catch (e) {
      setSaveError(apiErrorMessage(e));
    }
  }

  function onLogout() {
    Alert.alert('Log out?', 'You can sign back in any time.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Log out', style: 'destructive', onPress: () => useAuthStore.getState().logout() },
    ]);
  }

  if (isLoading) {
    return (
      <Screen>
        <View style={styles.centerBox}>
          <ActivityIndicator color={colors.primary} size="large" />
        </View>
      </Screen>
    );
  }

  if (isError || !account) {
    return (
      <Screen>
        <View style={styles.centerBox}>
          <ErrorText>{apiErrorMessage(error)}</ErrorText>
        </View>
      </Screen>
    );
  }

  return (
    <Screen>
      <ScrollView contentContainerStyle={styles.body} keyboardShouldPersistTaps="handled">
        <Text style={styles.title}>Settings</Text>

        <Card style={styles.card}>
          <Text style={styles.name}>{account.displayName || account.userName}</Text>
          {account.email ? <Muted>{account.email}</Muted> : null}
          <View style={styles.levelRow}>
            <View style={styles.levelBadge}>
              <Text style={styles.levelBadgeText}>Lv {account.level.level}</Text>
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.levelTitle}>{account.level.title}</Text>
              <View style={styles.progressTrack}>
                <View style={[styles.progressFill, { width: `${account.level.progressPct}%` }]} />
              </View>
              <Muted>
                {account.xp} XP · {account.level.xpToNext} to next level
              </Muted>
            </View>
          </View>
          <View style={styles.streakRow}>
            <Ionicons name="flame" size={16} color={colors.accent} />
            <Text style={styles.metaText}>
              {account.currentStreak} day streak · best {account.longestStreak}
            </Text>
          </View>
        </Card>

        <Card style={styles.card}>
          <Label>Display name</Label>
          <Input value={displayName} onChangeText={setDisplayName} placeholder="Your name" />
        </Card>

        <Card style={styles.card}>
          <Text style={styles.sectionTitle}>Reminders</Text>

          <View style={styles.toggleRow}>
            <Text style={styles.toggleLabel}>Morning reminder</Text>
            <Switch
              value={morningOn}
              onValueChange={setMorningOn}
              trackColor={{ false: colors.border, true: colors.brand1 }}
              thumbColor="#fff"
            />
          </View>
          {morningOn ? (
            <Pressable style={styles.pickerField} onPress={() => setPicker('morning')}>
              <Ionicons name="time-outline" size={18} color={colors.muted} />
              <Text style={styles.pickerText}>{morningTime}</Text>
            </Pressable>
          ) : null}

          <View style={[styles.toggleRow, { marginTop: spacing(3) }]}>
            <Text style={styles.toggleLabel}>End-of-day summary</Text>
            <Switch
              value={eodOn}
              onValueChange={setEodOn}
              trackColor={{ false: colors.border, true: colors.brand1 }}
              thumbColor="#fff"
            />
          </View>
          {eodOn ? (
            <Pressable style={styles.pickerField} onPress={() => setPicker('eod')}>
              <Ionicons name="time-outline" size={18} color={colors.muted} />
              <Text style={styles.pickerText}>{eodTime}</Text>
            </Pressable>
          ) : null}

          <View style={[styles.toggleRow, { marginTop: spacing(3) }]}>
            <Text style={styles.toggleLabel}>Auto carry-forward unfinished tasks</Text>
            <Switch
              value={carryForward}
              onValueChange={setCarryForward}
              trackColor={{ false: colors.border, true: colors.brand1 }}
              thumbColor="#fff"
            />
          </View>

          <View style={[styles.toggleRow, { marginTop: spacing(3) }]}>
            <Text style={styles.toggleLabel}>Speak reminders aloud</Text>
            <Switch
              value={speak}
              onValueChange={setSpeak}
              trackColor={{ false: colors.border, true: colors.brand1 }}
              thumbColor="#fff"
            />
          </View>

          <ErrorText>{saveError}</ErrorText>
          {saved && !saveError ? <Muted>Saved.</Muted> : null}
          <View style={{ height: spacing(3) }} />
          <Button title="Save changes" onPress={onSave} loading={updateSettings.isPending} />
        </Card>

        <Card style={styles.card}>
          <Text style={styles.sectionTitle}>Tools</Text>
          <Pressable style={styles.linkRow} onPress={() => router.push('/(app)/route-planner')}>
            <Ionicons name="navigate-outline" size={16} color={colors.primary} />
            <Text style={styles.linkText}>Route Planner</Text>
          </Pressable>
          <Pressable style={styles.linkRow} onPress={() => router.push('/(app)/assistant')}>
            <Ionicons name="sparkles-outline" size={16} color={colors.primary} />
            <Text style={styles.linkText}>Assistant (quick add)</Text>
          </Pressable>
          <Pressable style={styles.linkRow} onPress={() => router.push('/(app)/history')}>
            <Ionicons name="time-outline" size={16} color={colors.primary} />
            <Text style={styles.linkText}>Task history</Text>
          </Pressable>
        </Card>

        <Card style={styles.card}>
          <Text style={styles.sectionTitle}>More on the web</Text>
          <Muted>
            Attachments, invitations, and workspaces aren&rsquo;t available in the app yet — manage
            them from a browser at:
          </Muted>
          <Pressable style={styles.linkRow} onPress={() => Linking.openURL(siteUrl)}>
            <Ionicons name="globe-outline" size={16} color={colors.primary} />
            <Text style={styles.linkText}>{siteUrl}</Text>
          </Pressable>
        </Card>

        <Button title="Log out" variant="danger" onPress={onLogout} />
        <View style={{ height: spacing(10) }} />
      </ScrollView>

      {picker ? (
        <DateTimePicker
          value={new Date(`2000-01-01T${picker === 'morning' ? morningTime : eodTime}:00`)}
          mode="time"
          is24Hour
          onChange={(event, date) => {
            const target = picker;
            setPicker(null);
            if (event.type === 'set' && date && target) {
              if (target === 'morning') setMorningTime(toTimeString(date));
              else setEodTime(toTimeString(date));
            }
          }}
        />
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  centerBox: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: spacing(6) },
  body: { padding: spacing(5) },
  title: { color: colors.text, fontSize: 24, fontWeight: '800', marginBottom: spacing(4) },
  card: { marginBottom: spacing(4) },
  name: { color: colors.text, fontSize: 18, fontWeight: '700' },
  levelRow: { flexDirection: 'row', alignItems: 'center', gap: spacing(3), marginTop: spacing(4) },
  levelBadge: {
    backgroundColor: colors.brand1 + '22',
    borderColor: colors.brand1,
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: spacing(3),
    paddingVertical: spacing(1.5),
  },
  levelBadgeText: { color: colors.primary, fontWeight: '700', fontSize: 13 },
  levelTitle: { color: colors.text, fontWeight: '600', fontSize: 14 },
  progressTrack: {
    height: 6,
    borderRadius: 3,
    backgroundColor: colors.surface2,
    marginVertical: spacing(1.5),
    overflow: 'hidden',
  },
  progressFill: { height: '100%', backgroundColor: colors.brand1, borderRadius: 3 },
  streakRow: { flexDirection: 'row', alignItems: 'center', gap: spacing(1.5), marginTop: spacing(3) },
  metaText: { color: colors.muted, fontSize: 13 },
  sectionTitle: { color: colors.text, fontSize: 16, fontWeight: '700', marginBottom: spacing(2) },
  toggleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  toggleLabel: { color: colors.text, fontSize: 14, flex: 1, marginRight: spacing(3) },
  pickerField: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(2),
    height: 44,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface2,
    paddingHorizontal: spacing(3.5),
    marginTop: spacing(2),
  },
  pickerText: { color: colors.text, fontSize: 15 },
  linkRow: { flexDirection: 'row', alignItems: 'center', gap: spacing(2), marginTop: spacing(2.5) },
  linkText: { color: colors.primary, fontSize: 14, fontWeight: '600' },
});
