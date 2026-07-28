import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { Modal, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { priorityMeta, type TaskDto } from '@/features/tasks/api';
import { useBoard, useSetStatus, type BoardDto } from '@/features/tasks/views-api';

const { colors, radius, spacing } = theme;

const COLUMNS = [
  { key: 'pending', status: 0, label: 'Pending', icon: 'ellipse-outline' },
  { key: 'inProgress', status: 3, label: 'In Progress', icon: 'play-circle-outline' },
  { key: 'completed', status: 1, label: 'Completed', icon: 'checkmark-circle-outline' },
  { key: 'carriedForward', status: 2, label: 'Carried', icon: 'arrow-redo-outline' },
] as const satisfies ReadonlyArray<{ key: keyof BoardDto; status: number; label: string; icon: string }>;

export default function BoardScreen() {
  const { data, isLoading, isError, error, refetch, isRefetching } = useBoard();
  const setStatus = useSetStatus();
  const [active, setActive] = useState<TaskDto | null>(null);

  const columns = COLUMNS.map((meta) => ({ meta, tasks: data?.[meta.key] ?? [] }));
  const isEmpty = !isLoading && !isError && columns.every((c) => c.tasks.length === 0);

  return (
    <Screen>
      <View style={styles.header}>
        <Text style={styles.title}>Board</Text>
      </View>

      {isLoading ? (
        <View style={styles.empty}>
          <Muted>Loading your board…</Muted>
        </View>
      ) : isError ? (
        <View style={styles.empty}>
          <Muted>{apiErrorMessage(error)}</Muted>
        </View>
      ) : isEmpty ? (
        <View style={styles.empty}>
          <Ionicons name="grid-outline" size={40} color={colors.accent} />
          <Text style={styles.emptyTitle}>Nothing on the board</Text>
          <Muted>Tasks planned for today will show up here.</Muted>
        </View>
      ) : (
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.board}
          refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.primary} />}
        >
          {columns.map((col) => (
            <View key={col.meta.key} style={styles.column}>
              <View style={styles.columnHeader}>
                <Ionicons name={col.meta.icon} size={16} color={colors.muted} />
                <Text style={styles.columnTitle}>{col.meta.label}</Text>
                <View style={styles.countBadge}>
                  <Text style={styles.countText}>{col.tasks.length}</Text>
                </View>
              </View>
              <ScrollView
                style={styles.columnBody}
                contentContainerStyle={styles.columnList}
                showsVerticalScrollIndicator={false}
              >
                {col.tasks.length === 0 ? (
                  <Muted>No tasks</Muted>
                ) : (
                  col.tasks.map((t) => <BoardCard key={t.id} task={t} onPress={() => setActive(t)} />)
                )}
              </ScrollView>
            </View>
          ))}
        </ScrollView>
      )}

      <Modal visible={active != null} transparent animationType="fade" onRequestClose={() => setActive(null)}>
        <Pressable style={styles.backdrop} onPress={() => setActive(null)}>
          <Pressable style={styles.sheet} onPress={() => {}}>
            <Text style={styles.sheetTitle} numberOfLines={2}>
              {active?.title}
            </Text>
            <Muted>Move to…</Muted>
            <View style={{ height: spacing(2) }} />
            {COLUMNS.filter((m) => m.status !== active?.status).map((m) => (
              <Pressable
                key={m.key}
                style={({ pressed }) => [styles.option, pressed && { opacity: 0.7 }]}
                disabled={setStatus.isPending}
                onPress={() => {
                  if (active) setStatus.mutate({ id: active.id, status: m.status });
                  setActive(null);
                }}
              >
                <Ionicons name={m.icon} size={18} color={colors.primary} />
                <Text style={styles.optionText}>{m.label}</Text>
              </Pressable>
            ))}
            <Pressable style={styles.cancel} onPress={() => setActive(null)}>
              <Text style={styles.cancelText}>Cancel</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>
    </Screen>
  );
}

function BoardCard({ task, onPress }: { task: TaskDto; onPress: () => void }) {
  const priority = priorityMeta[task.priority] ?? priorityMeta[1];
  return (
    <Pressable style={({ pressed }) => [styles.card, pressed && { opacity: 0.8 }]} onPress={onPress}>
      <Text style={styles.cardTitle} numberOfLines={2}>
        {task.title}
      </Text>
      <View style={styles.cardMetaRow}>
        <View style={[styles.pill, { borderColor: priority.color }]}>
          <Text style={[styles.pillText, { color: priority.color }]}>{priority.label}</Text>
        </View>
        {task.dueTime ? (
          <View style={styles.metaItem}>
            <Ionicons name="time-outline" size={12} color={colors.muted} />
            <Text style={styles.metaText}>{task.dueTime}</Text>
          </View>
        ) : null}
      </View>
      {task.categoryName ? (
        <View style={styles.metaItem}>
          <View style={[styles.dot, { backgroundColor: task.categoryColor ?? colors.primary }]} />
          <Text style={styles.metaText} numberOfLines={1}>
            {task.categoryName}
          </Text>
        </View>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  header: {
    paddingHorizontal: spacing(5),
    paddingTop: spacing(3),
    paddingBottom: spacing(3),
  },
  title: { color: colors.text, fontSize: 24, fontWeight: '800' },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(8) },
  emptyTitle: { color: colors.text, fontSize: 18, fontWeight: '700' },
  board: { paddingHorizontal: spacing(4), paddingBottom: spacing(6), gap: spacing(3) },
  column: {
    width: 260,
    marginHorizontal: spacing(1),
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.md,
    padding: spacing(3),
    maxHeight: '100%',
  },
  columnHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing(1.5), marginBottom: spacing(3) },
  columnTitle: { flex: 1, color: colors.text, fontSize: 14, fontWeight: '700' },
  countBadge: {
    backgroundColor: colors.surface2,
    borderRadius: 999,
    paddingHorizontal: spacing(2),
    paddingVertical: 1,
  },
  countText: { color: colors.muted, fontSize: 12, fontWeight: '700' },
  columnBody: {},
  columnList: { gap: spacing(2.5) },
  card: {
    backgroundColor: colors.surface2,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.sm,
    padding: spacing(3),
    gap: spacing(1.5),
  },
  cardTitle: { color: colors.text, fontSize: 14, fontWeight: '600' },
  cardMetaRow: { flexDirection: 'row', alignItems: 'center', gap: spacing(2), flexWrap: 'wrap' },
  pill: { borderWidth: 1, borderRadius: 999, paddingHorizontal: spacing(2), paddingVertical: 1 },
  pillText: { fontSize: 10, fontWeight: '700' },
  metaItem: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  metaText: { color: colors.muted, fontSize: 11 },
  dot: { width: 7, height: 7, borderRadius: 4 },
  backdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  sheet: {
    backgroundColor: colors.surface,
    borderTopLeftRadius: radius.md,
    borderTopRightRadius: radius.md,
    padding: spacing(5),
    gap: spacing(1),
  },
  sheetTitle: { color: colors.text, fontSize: 16, fontWeight: '700' },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(3),
    paddingVertical: spacing(3.5),
    borderBottomColor: colors.border,
    borderBottomWidth: 1,
  },
  optionText: { color: colors.text, fontSize: 15, fontWeight: '600' },
  cancel: { alignItems: 'center', paddingTop: spacing(4), paddingBottom: spacing(2) },
  cancelText: { color: colors.danger, fontSize: 15, fontWeight: '700' },
});
