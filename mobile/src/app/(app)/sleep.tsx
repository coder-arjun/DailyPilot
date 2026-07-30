import DateTimePicker from '@react-native-community/datetimepicker';
import { Ionicons } from '@expo/vector-icons';
import React, { useEffect, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { Button, Card, ErrorText, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { useSleepDashboard, useSleepEntry, useSleepInsights, useUpsertSleepEntry } from '@/features/sleep/api';

const { colors, radius, spacing } = theme;

const pad = (n: number) => String(n).padStart(2, '0');
const toDateString = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const toTimeString = (d: Date) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;

function addDays(dateStr: string, days: number) {
  const d = new Date(`${dateStr}T00:00:00`);
  d.setDate(d.getDate() + days);
  return toDateString(d);
}

/** Times are plain "HH:mm" strings (no date) — anchor them to an arbitrary fixed
 * day so the time picker has a valid Date to render/edit. */
function timeToDate(time: string | null | undefined, fallback = '22:00') {
  const t = time || fallback;
  return new Date(`2000-01-01T${t}:00`);
}

/** Mirrors the web SaveMorning fallback: when no evening entry exists yet, synthesize
 * a sane bed time 8h before wake time so the (required) bedTime field is never blank. */
function subtractHours(time: string, hours: number) {
  const [h, m] = time.split(':').map(Number);
  const total = (((h * 60 + m - hours * 60) % 1440) + 1440) % 1440;
  return `${pad(Math.floor(total / 60))}:${pad(total % 60)}`;
}

function fmtDuration(minutes: number | null) {
  if (minutes == null) return '—';
  return `${Math.floor(minutes / 60)}h ${pad(minutes % 60)}m`;
}

function fmtTime12(time: string | null) {
  if (!time) return '—';
  const [h, m] = time.split(':').map(Number);
  const period = h >= 12 ? 'PM' : 'AM';
  const hour12 = h % 12 === 0 ? 12 : h % 12;
  return `${hour12}:${pad(m)} ${period}`;
}

function fmtNiceDate(dateStr: string) {
  return new Date(`${dateStr}T00:00:00`).toLocaleDateString(undefined, {
    weekday: 'short',
    day: '2-digit',
    month: 'short',
  });
}

const bandColors: Record<string, string> = {
  Excellent: '#bbf7d0',
  Good: '#bfdbfe',
  Fair: '#fde68a',
  Poor: '#fecaca',
};

type PickerTarget = 'bed' | 'est' | 'wake' | 'outOfBed' | null;

export default function SleepScreen() {
  const dashboard = useSleepDashboard();
  const insights = useSleepInsights();

  // Evening entries submitted at/after 12:00 device-local save to TOMORROW's wake date
  // (a bedtime logged tonight belongs to tomorrow morning); before 12:00 (an
  // already-past-midnight bedtime) they save to today's. Morning always saves to today.
  const now = new Date();
  const today = toDateString(now);
  const isEveningPeriod = now.getHours() >= 12;
  const eveningTargetDate = isEveningPeriod ? addDays(today, 1) : today;

  const eveningQuery = useSleepEntry(eveningTargetDate);
  const morningQuery = useSleepEntry(today);
  const upsertEvening = useUpsertSleepEntry();
  const upsertMorning = useUpsertSleepEntry();

  const [eveningOpen, setEveningOpen] = useState(isEveningPeriod);
  const [morningOpen, setMorningOpen] = useState(!isEveningPeriod);
  const [picker, setPicker] = useState<PickerTarget>(null);

  const [bedTime, setBedTime] = useState('22:30');
  const [estimatedSleepTime, setEstimatedSleepTime] = useState<string | null>(null);
  const [phoneBeforeBed, setPhoneBeforeBed] = useState(false);
  const [bedTimeEstimated, setBedTimeEstimated] = useState(false);
  const [eveningError, setEveningError] = useState('');

  useEffect(() => {
    const e = eveningQuery.data;
    if (e) {
      setBedTime(e.bedTime);
      setEstimatedSleepTime(e.estimatedSleepTime);
      setPhoneBeforeBed(e.phoneBeforeBed ?? false);
      setBedTimeEstimated(e.bedTimeEstimated ?? false);
    }
  }, [eveningQuery.data]);

  const [wakeTime, setWakeTime] = useState('07:00');
  const [timeOutOfBed, setTimeOutOfBed] = useState<string | null>(null);
  const [quality, setQuality] = useState(5);
  const [dreamRemembered, setDreamRemembered] = useState(false);
  const [morningError, setMorningError] = useState('');

  useEffect(() => {
    const m = morningQuery.data;
    if (m) {
      setWakeTime(m.wakeTime ?? '07:00');
      setTimeOutOfBed(m.timeOutOfBed);
      setQuality(m.quality ?? 5);
      setDreamRemembered(m.dreamRemembered ?? false);
    }
  }, [morningQuery.data]);

  async function onSaveEvening() {
    setEveningError('');
    try {
      await upsertEvening.mutateAsync({
        date: eveningTargetDate,
        bedTime,
        estimatedSleepTime: estimatedSleepTime || undefined,
        phoneBeforeBed,
        bedTimeEstimated: false, // the user just entered this — it's no longer a guess
      });
    } catch (e) {
      setEveningError(apiErrorMessage(e));
    }
  }

  async function onSaveMorning() {
    setMorningError('');
    const existing = morningQuery.data;
    try {
      await upsertMorning.mutateAsync({
        date: today,
        bedTime: existing?.bedTime ?? subtractHours(wakeTime, 8),
        estimatedSleepTime: existing?.estimatedSleepTime ?? undefined,
        wakeTime,
        timeOutOfBed: timeOutOfBed || undefined,
        quality,
        dreamRemembered,
        // Mirrors the web SaveMorning fallback: honestly flag a synthesized bed time so
        // the evening card never presents a guess as if the user had logged it.
        bedTimeEstimated: existing ? existing.bedTimeEstimated ?? false : true,
      });
    } catch (e) {
      setMorningError(apiErrorMessage(e));
    }
  }

  function onPickerChange(event: { type: string }, date?: Date) {
    const target = picker;
    setPicker(null);
    if (event.type !== 'set' || !date || !target) return;
    const t = toTimeString(date);
    if (target === 'bed') setBedTime(t);
    else if (target === 'est') setEstimatedSleepTime(t);
    else if (target === 'wake') setWakeTime(t);
    else if (target === 'outOfBed') setTimeOutOfBed(t);
  }

  function pickerValue(): Date {
    switch (picker) {
      case 'bed':
        return timeToDate(bedTime);
      case 'est':
        return timeToDate(estimatedSleepTime, bedTime);
      case 'wake':
        return timeToDate(wakeTime, '07:00');
      case 'outOfBed':
        return timeToDate(timeOutOfBed, wakeTime);
      default:
        return new Date();
    }
  }

  const d = dashboard.data;
  const stars = d?.todayScore != null ? Math.min(5, Math.max(0, Math.round(d.todayScore / 20))) : 0;
  const isRefetching = dashboard.isRefetching || insights.isRefetching;

  function onRefresh() {
    dashboard.refetch();
    insights.refetch();
    eveningQuery.refetch();
    morningQuery.refetch();
  }

  return (
    <Screen>
      <ScrollView
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={onRefresh} tintColor={colors.primary} />}
      >
        <Text style={styles.title}>Sleep</Text>

        {dashboard.isLoading ? (
          <View style={styles.empty}>
            <Muted>Loading your sleep dashboard…</Muted>
          </View>
        ) : dashboard.isError ? (
          <View style={styles.empty}>
            <Muted>{apiErrorMessage(dashboard.error)}</Muted>
          </View>
        ) : !d ? (
          <View style={styles.empty}>
            <Ionicons name="moon-outline" size={40} color={colors.accent} />
            <Muted>Log more nights to see your sleep dashboard.</Muted>
          </View>
        ) : (
          <>
            <Card style={styles.hero}>
              {d.todayScore != null ? (
                <>
                  <Text style={styles.heroLabel}>TODAY'S SLEEP</Text>
                  <Text style={styles.heroDuration}>{fmtDuration(d.todayDurationMinutes)}</Text>
                  <View style={styles.starRow}>
                    {[1, 2, 3, 4, 5].map((i) => (
                      <Ionicons key={i} name={i <= stars ? 'star' : 'star-outline'} size={20} color="#fbbf24" />
                    ))}
                  </View>
                  <View style={styles.scoreRow}>
                    <Text style={styles.scoreText}>Score: {d.todayScore}/100</Text>
                    <View style={styles.bandBadge}>
                      <Text style={[styles.bandText, { color: bandColors[d.todayBand ?? ''] ?? '#fff' }]}>
                        {d.todayBand}
                      </Text>
                    </View>
                  </View>
                </>
              ) : (
                <View style={styles.heroEmpty}>
                  <Ionicons name="moon-outline" size={32} color="#fff" />
                  <Text style={styles.heroEmptyText}>
                    No score yet today — log this morning's wake time and quality below.
                  </Text>
                </View>
              )}
            </Card>

            <View style={styles.statsGrid}>
              <Card style={styles.statCard}>
                <Muted>Average this week</Muted>
                <Text style={styles.statValue}>{fmtDuration(d.averageDurationMinutes)}</Text>
                {d.averageDurationMinutes == null ? <Text style={styles.statHint}>Log 2+ nights</Text> : null}
              </Card>
              <Card style={styles.statCard}>
                <Muted>Sleep debt</Muted>
                {d.completeNightCount === 0 ? (
                  <>
                    <Text style={styles.statValue}>—</Text>
                    <Text style={styles.statHint}>Log a night</Text>
                  </>
                ) : (
                  <>
                    <Text style={styles.statValue}>{fmtDuration(d.sleepDebtMinutes)}</Text>
                    <Text style={styles.statHint}>{d.sleepDebtMinutes === 0 ? 'On track' : 'over last 7 nights'}</Text>
                  </>
                )}
              </Card>
              <Card style={styles.statCard}>
                <Muted>Consistency</Muted>
                <Text style={styles.statValue}>{d.consistencyPct != null ? `${d.consistencyPct}%` : '—'}</Text>
                {d.consistencyPct == null ? <Text style={styles.statHint}>Log 3+ nights</Text> : null}
              </Card>
              <Card style={styles.statCard}>
                <Muted>Ideal bed time</Muted>
                <Text style={styles.statValue}>{fmtTime12(d.idealBedTime)}</Text>
                {d.idealBedTime == null ? <Text style={styles.statHint}>Log 2+ nights</Text> : null}
              </Card>
            </View>

            <Card style={styles.chartCard}>
              <Text style={styles.cardTitle}>Last 14 nights</Text>
              {d.recent.length === 0 ? (
                <Muted>Log a few nights to see your 14-day trend.</Muted>
              ) : (
                <View style={styles.chartRow}>
                  {d.recent.map((n) => {
                    const pct = Math.min(100, (n.durationMinutes / 600) * 100);
                    const barHeight = Math.max(6, Math.round((pct / 100) * 96));
                    const tint = n.quality >= 8 ? colors.success : n.quality >= 5 ? colors.accent : colors.danger;
                    const label = new Date(`${n.date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'narrow' });
                    return (
                      <View key={n.date} style={styles.barColumn}>
                        <View style={styles.barTrack}>
                          <View style={[styles.barFill, { height: barHeight, backgroundColor: tint }]} />
                        </View>
                        <Muted>{label}</Muted>
                      </View>
                    );
                  })}
                </View>
              )}
            </Card>

            <Card style={styles.insightsCard}>
              <Text style={styles.cardTitle}>Weekly insights</Text>
              {insights.isLoading ? (
                <Muted>Loading insights…</Muted>
              ) : insights.data && insights.data.weekly.length > 0 ? (
                insights.data.weekly.map((line, i) => (
                  <View key={i} style={styles.insightRow}>
                    <Ionicons name="ellipse" size={6} color={colors.muted} style={styles.insightIcon} />
                    <Text style={styles.insightText}>{line}</Text>
                  </View>
                ))
              ) : (
                <Muted>Log more nights to unlock weekly insights — three is enough to start.</Muted>
              )}
            </Card>

            {insights.data && insights.data.personalized.length > 0 ? (
              <Card style={styles.insightsCard}>
                <Text style={styles.cardTitle}>Personalized insights</Text>
                {insights.data.personalized.map((line, i) => (
                  <View key={i} style={styles.insightRow}>
                    <Ionicons name="bulb-outline" size={14} color={colors.accent} style={styles.insightIcon} />
                    <Text style={styles.insightText}>{line}</Text>
                  </View>
                ))}
              </Card>
            ) : null}

            <Card>
              <Pressable style={styles.cardHeader} onPress={() => setEveningOpen((v) => !v)}>
                <View style={styles.cardHeaderTitle}>
                  <Ionicons name="moon" size={16} color={colors.accent} />
                  <Text style={styles.cardTitle}>Before sleeping</Text>
                </View>
                <Ionicons name={eveningOpen ? 'chevron-up' : 'chevron-down'} size={18} color={colors.muted} />
              </Pressable>
              {eveningOpen ? (
                <View style={styles.cardBody}>
                  <Muted>Saves to the morning of {fmtNiceDate(eveningTargetDate)}.</Muted>

                  <Text style={styles.label}>Bed time</Text>
                  <Pressable style={styles.pickerField} onPress={() => setPicker('bed')}>
                    <Ionicons name="time-outline" size={18} color={colors.muted} />
                    <Text style={styles.pickerText}>{bedTime}</Text>
                  </Pressable>
                  {bedTimeEstimated ? (
                    <Muted>(estimated — no evening entry logged yet for this night; confirm or adjust before saving)</Muted>
                  ) : null}

                  <Text style={styles.label}>Estimated sleep time</Text>
                  <Pressable style={styles.pickerField} onPress={() => setPicker('est')}>
                    <Ionicons name="time-outline" size={18} color={colors.muted} />
                    <Text style={styles.pickerText}>{estimatedSleepTime ?? 'Bed time + 15 min'}</Text>
                  </Pressable>

                  <View style={styles.toggleRow}>
                    <View style={styles.toggleLabelRow}>
                      <Ionicons name="phone-portrait-outline" size={16} color={colors.muted} />
                      <Text style={styles.toggleLabel}>Used phone before bed</Text>
                    </View>
                    <Switch
                      value={phoneBeforeBed}
                      onValueChange={setPhoneBeforeBed}
                      trackColor={{ false: colors.border, true: colors.brand1 }}
                      thumbColor="#fff"
                    />
                  </View>

                  <ErrorText>{eveningError}</ErrorText>
                  <Button title="Save evening" onPress={onSaveEvening} loading={upsertEvening.isPending} style={styles.saveButton} />
                </View>
              ) : null}
            </Card>

            <Card>
              <Pressable style={styles.cardHeader} onPress={() => setMorningOpen((v) => !v)}>
                <View style={styles.cardHeaderTitle}>
                  <Ionicons name="sunny" size={16} color={colors.accent} />
                  <Text style={styles.cardTitle}>This morning</Text>
                </View>
                <Ionicons name={morningOpen ? 'chevron-up' : 'chevron-down'} size={18} color={colors.muted} />
              </Pressable>
              {morningOpen ? (
                <View style={styles.cardBody}>
                  <Muted>Saves to today, {fmtNiceDate(today)}.</Muted>

                  <Text style={styles.label}>Wake time</Text>
                  <Pressable style={styles.pickerField} onPress={() => setPicker('wake')}>
                    <Ionicons name="time-outline" size={18} color={colors.muted} />
                    <Text style={styles.pickerText}>{wakeTime}</Text>
                  </Pressable>

                  <Text style={styles.label}>Time out of bed</Text>
                  <Pressable style={styles.pickerField} onPress={() => setPicker('outOfBed')}>
                    <Ionicons name="time-outline" size={18} color={colors.muted} />
                    <Text style={styles.pickerText}>{timeOutOfBed ?? 'Same as wake time'}</Text>
                  </Pressable>

                  <Text style={styles.label}>Sleep quality</Text>
                  <View style={styles.qualityRow}>
                    {Array.from({ length: 10 }, (_, i) => i + 1).map((q) => (
                      <Pressable
                        key={q}
                        style={[styles.qualityPill, quality === q && styles.qualityPillActive]}
                        onPress={() => setQuality(q)}
                      >
                        <Text style={[styles.qualityText, quality === q && styles.qualityTextActive]}>{q}</Text>
                      </Pressable>
                    ))}
                  </View>

                  <View style={styles.toggleRow}>
                    <View style={styles.toggleLabelRow}>
                      <Ionicons name="cloud-outline" size={16} color={colors.muted} />
                      <Text style={styles.toggleLabel}>Remembered a dream</Text>
                    </View>
                    <Switch
                      value={dreamRemembered}
                      onValueChange={setDreamRemembered}
                      trackColor={{ false: colors.border, true: colors.brand1 }}
                      thumbColor="#fff"
                    />
                  </View>

                  <ErrorText>{morningError}</ErrorText>
                  <Button title="Save morning" onPress={onSaveMorning} loading={upsertMorning.isPending} style={styles.saveButton} />
                </View>
              ) : null}
            </Card>
          </>
        )}
      </ScrollView>

      {picker ? (
        <DateTimePicker value={pickerValue()} mode="time" is24Hour onChange={onPickerChange} />
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: { padding: spacing(5), paddingBottom: spacing(24), gap: spacing(4) },
  title: { color: colors.text, fontSize: 24, fontWeight: '800', marginBottom: spacing(2) },
  empty: { alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(16) },

  hero: { backgroundColor: colors.brand1, borderColor: colors.brand1 },
  heroLabel: { color: 'rgba(255,255,255,0.85)', fontSize: 12, fontWeight: '700', letterSpacing: 1 },
  heroDuration: { color: '#fff', fontSize: 32, fontWeight: '800', marginTop: spacing(1) },
  starRow: { flexDirection: 'row', gap: spacing(0.5), marginTop: spacing(3), marginBottom: spacing(2) },
  scoreRow: { flexDirection: 'row', alignItems: 'center', gap: spacing(3), flexWrap: 'wrap' },
  scoreText: { color: '#fff', fontWeight: '700', fontSize: 14 },
  bandBadge: { backgroundColor: 'rgba(255,255,255,0.16)', borderRadius: 999, paddingHorizontal: spacing(3), paddingVertical: spacing(1) },
  bandText: { fontWeight: '700', fontSize: 11, textTransform: 'uppercase', letterSpacing: 0.5 },
  heroEmpty: { alignItems: 'center', gap: spacing(2), paddingVertical: spacing(2) },
  heroEmptyText: { color: 'rgba(255,255,255,0.92)', fontSize: 14, textAlign: 'center' },

  statsGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing(3) },
  statCard: { flexBasis: '47%', flexGrow: 1, gap: spacing(1) },
  statValue: { color: colors.text, fontSize: 20, fontWeight: '800' },
  statHint: { color: colors.muted, fontSize: 11 },

  chartCard: { gap: spacing(3) },
  cardTitle: { color: colors.text, fontSize: 16, fontWeight: '700' },
  chartRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-end' },
  barColumn: { alignItems: 'center', gap: spacing(1), flex: 1 },
  barTrack: { height: 96, width: 14, justifyContent: 'flex-end' },
  barFill: { width: '100%', borderRadius: 5 },

  insightsCard: { gap: spacing(2) },
  insightRow: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing(2) },
  insightIcon: { marginTop: spacing(1) },
  insightText: { color: colors.text, fontSize: 14, flex: 1, lineHeight: 20 },

  cardHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  cardHeaderTitle: { flexDirection: 'row', alignItems: 'center', gap: spacing(2) },
  cardBody: { marginTop: spacing(3), gap: 0 },

  label: { color: colors.muted, fontSize: 13, fontWeight: '600', marginBottom: spacing(1.5), marginTop: spacing(3) },
  pickerField: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(2),
    height: 48,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface2,
    paddingHorizontal: spacing(3.5),
  },
  pickerText: { color: colors.text, fontSize: 15, flex: 1 },

  qualityRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing(2) },
  qualityPill: {
    width: 36,
    height: 36,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  qualityPillActive: { borderColor: colors.primary, backgroundColor: colors.primary + '22' },
  qualityText: { color: colors.muted, fontWeight: '700', fontSize: 13 },
  qualityTextActive: { color: colors.primary },

  toggleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: spacing(4) },
  toggleLabelRow: { flexDirection: 'row', alignItems: 'center', gap: spacing(2) },
  toggleLabel: { color: colors.text, fontSize: 14, fontWeight: '600' },

  saveButton: { marginTop: spacing(5) },
});
