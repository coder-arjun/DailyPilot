import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Card, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { useDashboard } from '@/features/dashboard/api';

const { colors, spacing } = theme;

export default function DashboardScreen() {
  const { data, isLoading, isError, error, refetch, isRefetching } = useDashboard();

  return (
    <Screen>
      <ScrollView
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.primary} />
        }
      >
        <Text style={styles.title}>Dashboard</Text>

        {isLoading ? (
          <View style={styles.empty}>
            <Muted>Loading your dashboard…</Muted>
          </View>
        ) : isError ? (
          <View style={styles.empty}>
            <Muted>{apiErrorMessage(error)}</Muted>
          </View>
        ) : !data ? (
          <View style={styles.empty}>
            <Ionicons name="stats-chart-outline" size={40} color={colors.accent} />
            <Muted>Nothing to show yet.</Muted>
          </View>
        ) : (
          <>
            <View style={styles.statsRow}>
              <Card style={styles.statCard}>
                <Ionicons name="checkmark-done-circle-outline" size={22} color={colors.success} />
                <Text style={styles.statValue}>
                  {data.todayCompleted}/{data.todayTotal}
                </Text>
                <Muted>Today</Muted>
              </Card>
              <Card style={styles.statCard}>
                <Ionicons name="flame" size={22} color={colors.accent} />
                <Text style={styles.statValue}>{data.currentStreak}</Text>
                <Muted>Streak (best {data.longestStreak})</Muted>
              </Card>
            </View>

            <Card style={styles.levelCard}>
              <View style={styles.levelHeader}>
                <View>
                  <Text style={styles.levelTitle}>
                    Level {data.level.level} · {data.level.title}
                  </Text>
                  <Muted>{data.level.xp} XP total</Muted>
                </View>
                <Text style={styles.levelPct}>{data.level.progressPct}%</Text>
              </View>
              <View style={styles.progressTrack}>
                <View
                  style={[
                    styles.progressFill,
                    { width: `${Math.min(100, Math.max(0, data.level.progressPct))}%` },
                  ]}
                />
              </View>
              <Muted>{data.level.xpToNext} XP to next level</Muted>
            </Card>

            <Card style={styles.chartCard}>
              <Text style={styles.chartTitle}>Last 7 days</Text>
              <View style={styles.chartRow}>
                {data.trend.map((p) => {
                  const pct = p.total === 0 ? 0 : p.completed / p.total;
                  const barHeight = Math.max(4, Math.round(pct * 96));
                  const label = new Date(`${p.date}T00:00:00`).toLocaleDateString(undefined, {
                    weekday: 'short',
                  });
                  return (
                    <View key={p.date} style={styles.barColumn}>
                      <Text style={styles.barCount}>{p.completed}</Text>
                      <View style={styles.barTrack}>
                        <View
                          style={[
                            styles.barFill,
                            { height: barHeight, backgroundColor: p.total === 0 ? colors.border : colors.brand1 },
                          ]}
                        />
                      </View>
                      <Muted>{label}</Muted>
                    </View>
                  );
                })}
              </View>
            </Card>
          </>
        )}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: { padding: spacing(5), paddingBottom: spacing(24), gap: spacing(4) },
  title: { color: colors.text, fontSize: 24, fontWeight: '800', marginBottom: spacing(2) },
  empty: { alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(16) },
  statsRow: { flexDirection: 'row', gap: spacing(3) },
  statCard: { flex: 1, alignItems: 'center', gap: spacing(1) },
  statValue: { color: colors.text, fontSize: 20, fontWeight: '800' },
  levelCard: { gap: spacing(2) },
  levelHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  levelTitle: { color: colors.text, fontSize: 16, fontWeight: '700' },
  levelPct: { color: colors.primary, fontSize: 16, fontWeight: '800' },
  progressTrack: { height: 10, borderRadius: 999, backgroundColor: colors.surface2, overflow: 'hidden' },
  progressFill: { height: '100%', backgroundColor: colors.brand1, borderRadius: 999 },
  chartCard: { gap: spacing(3) },
  chartTitle: { color: colors.text, fontSize: 16, fontWeight: '700' },
  chartRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-end' },
  barColumn: { alignItems: 'center', gap: spacing(1), flex: 1 },
  barCount: { color: colors.muted, fontSize: 11, fontWeight: '600' },
  barTrack: { height: 96, width: 18, justifyContent: 'flex-end' },
  barFill: { width: '100%', borderRadius: 6 },
});
