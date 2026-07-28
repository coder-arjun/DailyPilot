import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { theme } from '@/lib/theme';
import { priorityMeta, type TaskDto } from './api';

const { colors, radius, spacing } = theme;

type Props = {
  task: TaskDto;
  onToggle: (task: TaskDto) => void;
  onPress: (task: TaskDto) => void;
};

export function TaskRow({ task, onToggle, onPress }: Props) {
  const priority = priorityMeta[task.priority] ?? priorityMeta[1];
  return (
    <Pressable style={({ pressed }) => [styles.row, pressed && { opacity: 0.8 }]} onPress={() => onPress(task)}>
      <Pressable
        accessibilityRole="checkbox"
        accessibilityState={{ checked: task.isCompleted }}
        hitSlop={10}
        onPress={() => onToggle(task)}
      >
        <Ionicons
          name={task.isCompleted ? 'checkmark-circle' : 'ellipse-outline'}
          size={26}
          color={task.isCompleted ? colors.success : colors.muted}
        />
      </Pressable>

      <View style={styles.body}>
        <Text style={[styles.title, task.isCompleted && styles.done]} numberOfLines={2}>
          {task.title}
        </Text>
        <View style={styles.metaRow}>
          <View style={[styles.pill, { borderColor: priority.color }]}>
            <Text style={[styles.pillText, { color: priority.color }]}>{priority.label}</Text>
          </View>
          {task.categoryName ? (
            <View style={styles.metaItem}>
              <View style={[styles.dot, { backgroundColor: task.categoryColor ?? colors.primary }]} />
              <Text style={styles.metaText}>{task.categoryName}</Text>
            </View>
          ) : null}
          {task.dueTime ? (
            <View style={styles.metaItem}>
              <Ionicons name="time-outline" size={13} color={colors.muted} />
              <Text style={styles.metaText}>{task.dueTime}</Text>
            </View>
          ) : null}
          {task.carryForwardCount > 0 ? (
            <View style={styles.metaItem}>
              <Ionicons name="arrow-redo-outline" size={13} color={colors.accent} />
              <Text style={[styles.metaText, { color: colors.accent }]}>×{task.carryForwardCount}</Text>
            </View>
          ) : null}
        </View>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    gap: spacing(3),
    alignItems: 'flex-start',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.sm,
    padding: spacing(3.5),
  },
  body: { flex: 1, gap: spacing(1.5) },
  title: { color: colors.text, fontSize: 16, fontWeight: '600' },
  done: { color: colors.muted, textDecorationLine: 'line-through' },
  metaRow: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: spacing(2.5) },
  pill: { borderWidth: 1, borderRadius: 999, paddingHorizontal: spacing(2), paddingVertical: 1 },
  pillText: { fontSize: 11, fontWeight: '700' },
  metaItem: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  metaText: { color: colors.muted, fontSize: 12 },
  dot: { width: 8, height: 8, borderRadius: 4 },
});
