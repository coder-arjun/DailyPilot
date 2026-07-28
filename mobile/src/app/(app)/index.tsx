import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { useAuthStore } from '@/stores/authStore';
import { useTodayTasks, useToggleComplete, type TaskDto } from '@/features/tasks/api';
import { TaskRow } from '@/features/tasks/TaskRow';

const { colors, spacing } = theme;

export default function TodayScreen() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const { data: tasks, isLoading, isError, error, refetch, isRefetching } = useTodayTasks();
  const toggle = useToggleComplete();

  const open = tasks?.filter((t) => !t.isCompleted) ?? [];
  const done = tasks?.filter((t) => t.isCompleted) ?? [];
  const ordered: TaskDto[] = [...open, ...done];

  const today = new Date().toLocaleDateString(undefined, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });

  return (
    <Screen>
      <View style={styles.header}>
        <View>
          <Text style={styles.hello}>Hi {user?.displayName?.split(' ')[0] ?? 'there'}</Text>
          <Muted>{today}</Muted>
        </View>
        {tasks ? (
          <View style={styles.counter}>
            <Text style={styles.counterText}>
              {done.length}/{tasks.length}
            </Text>
          </View>
        ) : null}
      </View>

      <FlatList
        data={ordered}
        keyExtractor={(t) => String(t.id)}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={{ height: spacing(2.5) }} />}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.primary} />
        }
        renderItem={({ item }) => (
          <TaskRow
            task={item}
            onToggle={(t) => toggle.mutate(t)}
            onPress={(t) => router.push({ pathname: '/(app)/task-editor', params: { id: String(t.id) } })}
          />
        )}
        ListEmptyComponent={
          <View style={styles.empty}>
            {isLoading ? (
              <Muted>Loading your day…</Muted>
            ) : isError ? (
              <Muted>{apiErrorMessage(error)}</Muted>
            ) : (
              <>
                <Ionicons name="sunny-outline" size={40} color={colors.accent} />
                <Text style={styles.emptyTitle}>Nothing planned yet</Text>
                <Muted>Add your first task for today.</Muted>
              </>
            )}
          </View>
        }
      />

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Add task"
        style={({ pressed }) => [styles.fab, pressed && { transform: [{ scale: 0.96 }] }]}
        onPress={() => router.push('/(app)/task-editor')}
      >
        <Ionicons name="add" size={30} color="#fff" />
      </Pressable>
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: spacing(5),
    paddingTop: spacing(3),
    paddingBottom: spacing(4),
  },
  hello: { color: colors.text, fontSize: 24, fontWeight: '800' },
  counter: {
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: spacing(3.5),
    paddingVertical: spacing(1.5),
  },
  counterText: { color: colors.primary, fontWeight: '700' },
  list: { paddingHorizontal: spacing(5), paddingBottom: spacing(24), flexGrow: 1 },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing(2) },
  emptyTitle: { color: colors.text, fontSize: 18, fontWeight: '700' },
  fab: {
    position: 'absolute',
    right: spacing(5),
    bottom: spacing(6),
    width: 60,
    height: 60,
    borderRadius: 30,
    backgroundColor: colors.brand1,
    alignItems: 'center',
    justifyContent: 'center',
    elevation: 6,
  },
});
