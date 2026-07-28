import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { Button, Card, ErrorText, Input, Label, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import {
  habitColorPresets,
  useCreateHabit,
  useHabits,
  useToggleHabitCheckin,
  type HabitDto,
} from '@/features/habits/api';

const { colors, radius, spacing } = theme;

export default function HabitsScreen() {
  const { data: habits, isLoading, isError, error, refetch, isRefetching } = useHabits();
  const toggle = useToggleHabitCheckin();
  const createHabit = useCreateHabit();

  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [color, setColor] = useState(habitColorPresets[0]);
  const [targetPerWeek, setTargetPerWeek] = useState('3');
  const [formError, setFormError] = useState('');

  async function onCreate() {
    setFormError('');
    if (!name.trim()) {
      setFormError('Give the habit a name.');
      return;
    }
    const target = Math.min(7, Math.max(1, Number(targetPerWeek) || 3));
    try {
      await createHabit.mutateAsync({
        name: name.trim(),
        frequency: 0,
        targetPerWeek: target,
        color,
      });
      setName('');
      setColor(habitColorPresets[0]);
      setTargetPerWeek('3');
      setShowForm(false);
    } catch (e) {
      setFormError(apiErrorMessage(e));
    }
  }

  return (
    <Screen>
      <View style={styles.header}>
        <Text style={styles.title}>Habits</Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={showForm ? 'Close new habit form' : 'Add habit'}
          style={styles.headerButton}
          onPress={() => setShowForm((s) => !s)}
        >
          <Ionicons name={showForm ? 'close' : 'add'} size={24} color={colors.text} />
        </Pressable>
      </View>

      <FlatList
        data={habits ?? []}
        keyExtractor={(h) => String(h.id)}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={{ height: spacing(2.5) }} />}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.primary} />
        }
        ListHeaderComponent={
          showForm ? (
            <Card style={styles.formCard}>
              <Label>Name</Label>
              <Input value={name} onChangeText={setName} placeholder="e.g. Drink water" />

              <Label>Color</Label>
              <View style={styles.colorRow}>
                {habitColorPresets.map((c) => (
                  <Pressable
                    key={c}
                    accessibilityRole="button"
                    accessibilityLabel={`Color ${c}`}
                    onPress={() => setColor(c)}
                    style={[styles.colorSwatch, { backgroundColor: c }, color === c && styles.colorSwatchActive]}
                  />
                ))}
              </View>

              <Label>Weekly target (days)</Label>
              <Input
                value={targetPerWeek}
                onChangeText={setTargetPerWeek}
                keyboardType="number-pad"
                placeholder="3"
              />

              <ErrorText>{formError}</ErrorText>
              <View style={{ height: spacing(3) }} />
              <Button title="Add habit" onPress={onCreate} loading={createHabit.isPending} />
            </Card>
          ) : null
        }
        renderItem={({ item }) => <HabitCard habit={item} onToggle={(h) => toggle.mutate(h)} />}
        ListEmptyComponent={
          <View style={styles.empty}>
            {isLoading ? (
              <Muted>Loading your habits…</Muted>
            ) : isError ? (
              <Muted>{apiErrorMessage(error)}</Muted>
            ) : (
              <>
                <Ionicons name="flame-outline" size={40} color={colors.accent} />
                <Text style={styles.emptyTitle}>No habits yet</Text>
                <Muted>Add one to start building a streak.</Muted>
              </>
            )}
          </View>
        }
      />
    </Screen>
  );
}

function HabitCard({ habit, onToggle }: { habit: HabitDto; onToggle: (h: HabitDto) => void }) {
  const isWeekly = habit.frequency === 1;
  return (
    <View style={styles.row}>
      <View style={[styles.dot, { backgroundColor: habit.color }]} />
      <View style={styles.body}>
        <Text style={styles.name}>{habit.name}</Text>
        <View style={styles.metaRow}>
          <Ionicons name="flame" size={14} color={colors.accent} />
          <Text style={styles.metaText}>
            {habit.currentStreak} {isWeekly ? 'week' : 'day'}
            {habit.currentStreak === 1 ? '' : 's'}
          </Text>
          {isWeekly ? (
            <Text style={styles.metaText}>
              · {habit.thisWeekCount}/{habit.targetPerWeek} this week
            </Text>
          ) : null}
        </View>
      </View>
      <Pressable
        accessibilityRole="checkbox"
        accessibilityState={{ checked: habit.checkedToday }}
        hitSlop={10}
        onPress={() => onToggle(habit)}
      >
        <Ionicons
          name={habit.checkedToday ? 'checkmark-circle' : 'ellipse-outline'}
          size={30}
          color={habit.checkedToday ? colors.success : colors.muted}
        />
      </Pressable>
    </View>
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
  title: { color: colors.text, fontSize: 24, fontWeight: '800' },
  headerButton: {
    width: 40,
    height: 40,
    borderRadius: 20,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
  },
  list: { paddingHorizontal: spacing(5), paddingBottom: spacing(24), flexGrow: 1 },
  formCard: { marginBottom: spacing(4) },
  colorRow: { flexDirection: 'row', gap: spacing(2.5), flexWrap: 'wrap' },
  colorSwatch: { width: 32, height: 32, borderRadius: 16, borderWidth: 2, borderColor: 'transparent' },
  colorSwatchActive: { borderColor: colors.text },
  row: {
    flexDirection: 'row',
    gap: spacing(3),
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.sm,
    padding: spacing(3.5),
  },
  dot: { width: 14, height: 14, borderRadius: 7 },
  body: { flex: 1, gap: spacing(1) },
  name: { color: colors.text, fontSize: 16, fontWeight: '600' },
  metaRow: { flexDirection: 'row', alignItems: 'center', gap: spacing(1.5) },
  metaText: { color: colors.muted, fontSize: 12 },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(20) },
  emptyTitle: { color: colors.text, fontSize: 18, fontWeight: '700' },
});
