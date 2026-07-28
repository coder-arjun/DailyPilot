import DateTimePicker from '@react-native-community/datetimepicker';
import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams, useRouter } from 'expo-router';
import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Button, Card, ErrorText, Input, Label, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import {
  priorityMeta,
  useCategories,
  useCreateTask,
  useDeleteTask,
  useTask,
  useTodayTasks,
  useUpdateTask,
} from '@/features/tasks/api';

const { colors, radius, spacing } = theme;

const toDateString = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const toTimeString = (d: Date) =>
  `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;

export default function TaskEditorScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ id?: string }>();
  const editingId = params.id ? Number(params.id) : null;

  const { data: tasks } = useTodayTasks();
  const cacheHit = editingId ? tasks?.find((t) => t.id === editingId) : undefined;
  // Fallback for tasks not in the today cache (e.g. opened from search — could be any date).
  const taskQuery = useTask(editingId);
  const existing = cacheHit ?? taskQuery.data;

  // Still resolving the edit target: no cache hit yet, and the by-id fetch hasn't settled.
  const isResolvingExisting = editingId != null && !existing && taskQuery.isLoading;
  const resolveError = editingId != null && !existing && !taskQuery.isLoading && taskQuery.isError;

  const { data: categories } = useCategories();
  const createTask = useCreateTask();
  const updateTask = useUpdateTask();
  const deleteTask = useDeleteTask();

  const [title, setTitle] = useState('');
  const [notes, setNotes] = useState('');
  const [plannedDate, setPlannedDate] = useState(toDateString(new Date()));
  const [dueTime, setDueTime] = useState<string | null>(null);
  const [reminderTime, setReminderTime] = useState<string | null>(null);
  const [priority, setPriority] = useState(1);
  const [categoryId, setCategoryId] = useState<number | null>(null);
  const [error, setError] = useState('');
  const [picker, setPicker] = useState<'date' | 'due' | 'reminder' | null>(null);

  useEffect(() => {
    if (existing) {
      setTitle(existing.title);
      setNotes(existing.notes ?? '');
      setPlannedDate(existing.plannedDate);
      setDueTime(existing.dueTime);
      setReminderTime(existing.reminderTime);
      setPriority(existing.priority);
      setCategoryId(existing.categoryId);
    }
  }, [existing]);

  const busy = createTask.isPending || updateTask.isPending || deleteTask.isPending;

  async function onSave() {
    setError('');
    if (!title.trim()) {
      setError('Give the task a title.');
      return;
    }
    const body = {
      title: title.trim(),
      notes: notes.trim() || null,
      plannedDate,
      dueTime,
      reminderTime,
      priority,
      categoryId,
    };
    try {
      if (editingId) await updateTask.mutateAsync({ id: editingId, ...body });
      else await createTask.mutateAsync(body);
      router.back();
    } catch (e) {
      setError(apiErrorMessage(e));
    }
  }

  function onDelete() {
    if (!editingId) return;
    Alert.alert('Delete task?', 'This cannot be undone.', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Delete',
        style: 'destructive',
        onPress: async () => {
          try {
            await deleteTask.mutateAsync(editingId);
            router.back();
          } catch (e) {
            setError(apiErrorMessage(e));
          }
        },
      },
    ]);
  }

  return (
    <Screen>
      <View style={styles.header}>
        <Pressable hitSlop={10} onPress={() => router.back()} accessibilityLabel="Close editor">
          <Ionicons name="close" size={26} color={colors.muted} />
        </Pressable>
        <Text style={styles.headerTitle}>{editingId ? 'Edit task' : 'New task'}</Text>
        {editingId ? (
          <Pressable hitSlop={10} onPress={onDelete} accessibilityLabel="Delete task">
            <Ionicons name="trash-outline" size={22} color={colors.danger} />
          </Pressable>
        ) : (
          <View style={{ width: 22 }} />
        )}
      </View>

      {isResolvingExisting ? (
        <View style={styles.centerBox}>
          <ActivityIndicator color={colors.primary} size="large" />
        </View>
      ) : resolveError ? (
        <View style={styles.centerBox}>
          <ErrorText>{apiErrorMessage(taskQuery.error)}</ErrorText>
        </View>
      ) : (
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView contentContainerStyle={styles.body} keyboardShouldPersistTaps="handled">
          <Card>
            <Label>Title</Label>
            <Input value={title} onChangeText={setTitle} placeholder="What needs doing?" />

            <Label>Notes</Label>
            <Input
              value={notes}
              onChangeText={setNotes}
              placeholder="Optional details"
              multiline
              style={{ height: 88, textAlignVertical: 'top', paddingTop: spacing(2.5) }}
            />

            <Label>Date</Label>
            <Pressable style={styles.pickerField} onPress={() => setPicker('date')}>
              <Ionicons name="calendar-outline" size={18} color={colors.muted} />
              <Text style={styles.pickerText}>{plannedDate}</Text>
            </Pressable>

            <View style={styles.timeRow}>
              <View style={{ flex: 1 }}>
                <Label>Due time</Label>
                <Pressable style={styles.pickerField} onPress={() => setPicker('due')}>
                  <Ionicons name="time-outline" size={18} color={colors.muted} />
                  <Text style={styles.pickerText}>{dueTime ?? 'None'}</Text>
                  {dueTime ? (
                    <Pressable hitSlop={8} onPress={() => setDueTime(null)}>
                      <Ionicons name="close-circle" size={16} color={colors.muted} />
                    </Pressable>
                  ) : null}
                </Pressable>
              </View>
              <View style={{ flex: 1 }}>
                <Label>Reminder</Label>
                <Pressable style={styles.pickerField} onPress={() => setPicker('reminder')}>
                  <Ionicons name="notifications-outline" size={18} color={colors.muted} />
                  <Text style={styles.pickerText}>{reminderTime ?? 'None'}</Text>
                  {reminderTime ? (
                    <Pressable hitSlop={8} onPress={() => setReminderTime(null)}>
                      <Ionicons name="close-circle" size={16} color={colors.muted} />
                    </Pressable>
                  ) : null}
                </Pressable>
              </View>
            </View>

            <Label>Priority</Label>
            <View style={styles.segment}>
              {[0, 1, 2].map((p) => (
                <Pressable
                  key={p}
                  style={[styles.segmentItem, priority === p && { backgroundColor: priorityMeta[p].color + '22', borderColor: priorityMeta[p].color }]}
                  onPress={() => setPriority(p)}
                >
                  <Text style={[styles.segmentText, priority === p && { color: priorityMeta[p].color }]}>
                    {priorityMeta[p].label}
                  </Text>
                </Pressable>
              ))}
            </View>

            <Label>Category</Label>
            <View style={styles.segment}>
              <Pressable
                style={[styles.segmentItem, categoryId === null && styles.segmentActive]}
                onPress={() => setCategoryId(null)}
              >
                <Text style={[styles.segmentText, categoryId === null && { color: colors.primary }]}>None</Text>
              </Pressable>
              {categories?.map((c) => (
                <Pressable
                  key={c.id}
                  style={[styles.segmentItem, categoryId === c.id && { backgroundColor: c.color + '22', borderColor: c.color }]}
                  onPress={() => setCategoryId(c.id)}
                >
                  <Text style={[styles.segmentText, categoryId === c.id && { color: c.color }]}>{c.name}</Text>
                </Pressable>
              ))}
            </View>

            <ErrorText>{error}</ErrorText>
            <View style={{ height: spacing(5) }} />
            <Button title={editingId ? 'Save changes' : 'Add task'} onPress={onSave} loading={busy} />
          </Card>
        </ScrollView>
      </KeyboardAvoidingView>
      )}

      {picker === 'date' ? (
        <DateTimePicker
          value={new Date(plannedDate + 'T00:00:00')}
          mode="date"
          onChange={(event, date) => {
            setPicker(null);
            if (event.type === 'set' && date) setPlannedDate(toDateString(date));
          }}
        />
      ) : null}
      {picker === 'due' || picker === 'reminder' ? (
        <DateTimePicker
          value={new Date()}
          mode="time"
          is24Hour
          onChange={(event, date) => {
            const target = picker;
            setPicker(null);
            if (event.type === 'set' && date && target) {
              if (target === 'due') setDueTime(toTimeString(date));
              else setReminderTime(toTimeString(date));
            }
          }}
        />
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: spacing(5),
    paddingVertical: spacing(3),
  },
  headerTitle: { color: colors.text, fontSize: 18, fontWeight: '700' },
  body: { padding: spacing(5), paddingTop: 0 },
  centerBox: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: spacing(6) },
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
  timeRow: { flexDirection: 'row', gap: spacing(3) },
  segment: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing(2) },
  segmentItem: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 999,
    paddingHorizontal: spacing(3.5),
    paddingVertical: spacing(2),
    backgroundColor: colors.surface2,
  },
  segmentActive: { borderColor: colors.primary, backgroundColor: colors.primary + '22' },
  segmentText: { color: colors.muted, fontWeight: '600', fontSize: 13 },
});
