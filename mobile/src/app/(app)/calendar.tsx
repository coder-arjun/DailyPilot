import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { useToggleComplete, type TaskDto } from '@/features/tasks/api';
import { useCalendarMonth, type CalendarDayDto } from '@/features/tasks/views-api';
import { TaskRow } from '@/features/tasks/TaskRow';

const { colors, radius, spacing } = theme;

const WEEKDAY_LABELS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

function pad2(n: number) {
  return String(n).padStart(2, '0');
}

function isoDate(d: Date) {
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}

function monthKey(year: number, month: number) {
  return `${year}-${pad2(month)}`; // month is 1-12
}

/** 6 weeks (42 days) starting on the Monday on/before the 1st, mirroring the web Calendar's grid. */
function buildGrid(year: number, month: number): Date[] {
  const first = new Date(year, month - 1, 1);
  const offset = (first.getDay() + 6) % 7; // Mon=0..Sun=6
  const gridStart = new Date(year, month - 1, 1 - offset);
  return Array.from({ length: 42 }, (_, i) => new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i));
}

export default function CalendarScreen() {
  const router = useRouter();
  const now = useMemo(() => new Date(), []);
  const todayIso = isoDate(now);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1); // 1-12
  const [selected, setSelected] = useState(todayIso);

  const key = monthKey(year, month);
  const { data, isLoading, isError, error } = useCalendarMonth(key);
  const toggle = useToggleComplete();

  const dayLookup = useMemo(() => {
    const map = new Map<string, CalendarDayDto>();
    for (const d of data?.days ?? []) map.set(d.date, d);
    return map;
  }, [data]);

  const grid = useMemo(() => buildGrid(year, month), [year, month]);

  const selectedTasks = useMemo(
    () => (data?.tasks ?? []).filter((t) => t.plannedDate === selected),
    [data, selected],
  );

  const monthLabel = new Date(year, month - 1, 1).toLocaleDateString(undefined, {
    month: 'long',
    year: 'numeric',
  });

  function changeMonth(delta: number) {
    let m = month + delta;
    let y = year;
    if (m < 1) { m = 12; y -= 1; }
    if (m > 12) { m = 1; y += 1; }
    setYear(y);
    setMonth(m);
    const stillToday = y === now.getFullYear() && m === now.getMonth() + 1;
    setSelected(stillToday ? todayIso : `${monthKey(y, m)}-01`);
  }

  /** Selecting a leading/trailing overflow cell navigates the header to that day's
   * actual month too, so the fetched data (and dots) match what's rendered. */
  function selectDay(d: Date) {
    const cellYear = d.getFullYear();
    const cellMonth = d.getMonth() + 1;
    if (cellYear !== year || cellMonth !== month) {
      setYear(cellYear);
      setMonth(cellMonth);
    }
    setSelected(isoDate(d));
  }

  return (
    <Screen>
      <View style={styles.header}>
        <Text style={styles.title}>Calendar</Text>
      </View>

      <View style={styles.monthBar}>
        <Pressable accessibilityLabel="Previous month" hitSlop={10} onPress={() => changeMonth(-1)}>
          <Ionicons name="chevron-back" size={22} color={colors.text} />
        </Pressable>
        <Text style={styles.monthLabel}>{monthLabel}</Text>
        <Pressable accessibilityLabel="Next month" hitSlop={10} onPress={() => changeMonth(1)}>
          <Ionicons name="chevron-forward" size={22} color={colors.text} />
        </Pressable>
      </View>

      <View style={styles.weekdayRow}>
        {WEEKDAY_LABELS.map((w) => (
          <Text key={w} style={styles.weekdayLabel}>{w}</Text>
        ))}
      </View>

      {isError ? (
        <View style={styles.empty}>
          <Muted>{apiErrorMessage(error)}</Muted>
        </View>
      ) : (
        <View style={styles.grid}>
          {grid.map((d) => {
            const iso = isoDate(d);
            const inMonth = d.getMonth() + 1 === month;
            const isToday = iso === todayIso;
            const isSelected = iso === selected;
            const info = dayLookup.get(iso);
            const hasTasks = (info?.total ?? 0) > 0;
            return (
              <Pressable
                key={iso}
                style={[
                  styles.cell,
                  isSelected && styles.cellSelected,
                  isToday && !isSelected && styles.cellToday,
                ]}
                onPress={() => selectDay(d)}
              >
                <Text style={[styles.cellText, !inMonth && styles.cellTextOutside, isSelected && styles.cellTextSelected]}>
                  {d.getDate()}
                </Text>
                {hasTasks ? (
                  info!.completed >= info!.total ? (
                    <Ionicons name="checkmark-circle" size={10} color={colors.success} style={styles.cellIcon} />
                  ) : (
                    <View style={[styles.dot, isSelected && { backgroundColor: colors.text }]} />
                  )
                ) : (
                  <View style={styles.dotPlaceholder} />
                )}
              </Pressable>
            );
          })}
        </View>
      )}

      <View style={styles.listHeader}>
        <Text style={styles.listTitle}>
          {new Date(`${selected}T00:00:00`).toLocaleDateString(undefined, {
            weekday: 'long',
            day: 'numeric',
            month: 'long',
          })}
        </Text>
      </View>

      <FlatList
        data={selectedTasks}
        keyExtractor={(t) => String(t.id)}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={{ height: spacing(2.5) }} />}
        renderItem={({ item }) => (
          <TaskRow
            task={item}
            onToggle={(t) => toggle.mutate(t)}
            onPress={(t: TaskDto) => router.push({ pathname: '/(app)/task-editor', params: { id: String(t.id) } })}
          />
        )}
        ListEmptyComponent={
          <View style={styles.empty}>
            {isLoading ? (
              <Muted>Loading calendar…</Muted>
            ) : (
              <>
                <Ionicons name="calendar-outline" size={36} color={colors.accent} />
                <Muted>Nothing planned for this day.</Muted>
              </>
            )}
          </View>
        }
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: {
    paddingHorizontal: spacing(5),
    paddingTop: spacing(3),
    paddingBottom: spacing(2),
  },
  title: { color: colors.text, fontSize: 24, fontWeight: '800' },
  monthBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: spacing(5),
    paddingBottom: spacing(3),
  },
  monthLabel: { color: colors.text, fontSize: 16, fontWeight: '700' },
  weekdayRow: { flexDirection: 'row', paddingHorizontal: spacing(5) },
  weekdayLabel: { flex: 1, textAlign: 'center', color: colors.muted, fontSize: 12, fontWeight: '600' },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    paddingHorizontal: spacing(4),
    paddingTop: spacing(1),
  },
  cell: {
    width: `${100 / 7}%`,
    aspectRatio: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 2,
  },
  cellSelected: {
    backgroundColor: colors.brand1,
    borderRadius: radius.sm,
  },
  cellToday: {
    borderWidth: 1.5,
    borderColor: colors.primary,
    borderRadius: radius.sm,
  },
  cellText: { color: colors.text, fontSize: 14, fontWeight: '600' },
  cellTextOutside: { color: colors.muted, opacity: 0.5 },
  cellTextSelected: { color: '#fff' },
  cellIcon: { marginTop: 2 },
  dot: { width: 5, height: 5, borderRadius: 3, backgroundColor: colors.accent, marginTop: 3 },
  dotPlaceholder: { width: 5, height: 5, marginTop: 3 },
  listHeader: { paddingHorizontal: spacing(5), paddingTop: spacing(3), paddingBottom: spacing(2) },
  listTitle: { color: colors.text, fontSize: 16, fontWeight: '700' },
  list: { paddingHorizontal: spacing(5), paddingBottom: spacing(24), flexGrow: 1 },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(8) },
});
