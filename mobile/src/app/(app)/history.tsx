import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { Button, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { useHistory, type TaskHistoryDto } from '@/features/history/api';

const { colors, radius, spacing } = theme;

const actionColor: Record<string, string> = {
  Completed: colors.success,
  Created: colors.primary,
  Deleted: colors.danger,
  CarriedForward: colors.accent,
  Reopened: colors.muted,
};

function relativeTime(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime();
  const diffSec = Math.max(0, Math.round(diffMs / 1000));
  if (diffSec < 60) return 'just now';
  const diffMin = Math.round(diffSec / 60);
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHour = Math.round(diffMin / 60);
  if (diffHour < 24) return `${diffHour}h ago`;
  const diffDay = Math.round(diffHour / 24);
  if (diffDay < 7) return `${diffDay}d ago`;
  const diffWeek = Math.round(diffDay / 7);
  if (diffWeek < 5) return `${diffWeek}w ago`;
  const diffMonth = Math.round(diffDay / 30);
  if (diffMonth < 12) return `${diffMonth}mo ago`;
  return `${Math.round(diffDay / 365)}y ago`;
}

export default function HistoryScreen() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, error, isFetching, isRefetching, refetch } = useHistory(page);

  return (
    <Screen>
      <View style={styles.header}>
        <Pressable hitSlop={10} onPress={() => router.back()} accessibilityLabel="Back">
          <Ionicons name="arrow-back" size={22} color={colors.text} />
        </Pressable>
        <Text style={styles.title}>History</Text>
      </View>

      <FlatList
        data={data?.items ?? []}
        keyExtractor={(h) => String(h.id)}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={{ height: spacing(2.5) }} />}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.primary} />
        }
        renderItem={({ item }) => <HistoryRow item={item} />}
        ListEmptyComponent={
          <View style={styles.empty}>
            {isLoading ? (
              <Muted>Loading history…</Muted>
            ) : isError ? (
              <Muted>{apiErrorMessage(error)}</Muted>
            ) : (
              <>
                <Ionicons name="time-outline" size={40} color={colors.accent} />
                <Text style={styles.emptyTitle}>No activity yet</Text>
                <Muted>Task changes will show up here.</Muted>
              </>
            )}
          </View>
        }
        ListFooterComponent={
          data && data.totalPages > 1 ? (
            <View style={styles.pager}>
              <Button
                title="Prev"
                variant="ghost"
                disabled={page <= 1 || isFetching}
                onPress={() => setPage((p) => Math.max(1, p - 1))}
                style={{ flex: 1, marginRight: spacing(2) }}
              />
              <Muted>
                Page {data.page} of {data.totalPages}
              </Muted>
              <Button
                title="Next"
                variant="ghost"
                disabled={page >= data.totalPages || isFetching}
                onPress={() => setPage((p) => p + 1)}
                style={{ flex: 1, marginLeft: spacing(2) }}
              />
            </View>
          ) : null
        }
      />
    </Screen>
  );
}

function HistoryRow({ item }: { item: TaskHistoryDto }) {
  const color = actionColor[item.action] ?? colors.muted;
  return (
    <View style={styles.row}>
      <View style={[styles.badge, { borderColor: color }]}>
        <Text style={[styles.badgeText, { color }]}>{item.action}</Text>
      </View>
      <View style={styles.body}>
        <Text style={styles.rowTitle} numberOfLines={1}>
          {item.taskTitle}
        </Text>
        {item.details ? <Muted>{item.details}</Muted> : null}
      </View>
      <Muted>{relativeTime(item.timestamp)}</Muted>
    </View>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(3),
    paddingHorizontal: spacing(5),
    paddingTop: spacing(3),
    paddingBottom: spacing(4),
  },
  title: { color: colors.text, fontSize: 24, fontWeight: '800' },
  list: { paddingHorizontal: spacing(5), paddingBottom: spacing(10), flexGrow: 1 },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(16) },
  emptyTitle: { color: colors.text, fontSize: 18, fontWeight: '700' },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(3),
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.sm,
    padding: spacing(3.5),
  },
  badge: { borderWidth: 1, borderRadius: 999, paddingHorizontal: spacing(2), paddingVertical: 2 },
  badgeText: { fontSize: 11, fontWeight: '700' },
  body: { flex: 1, gap: spacing(1) },
  rowTitle: { color: colors.text, fontSize: 15, fontWeight: '600' },
  pager: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: spacing(4),
  },
});
