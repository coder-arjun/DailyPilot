import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useEffect, useState } from 'react';
import { Alert, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { Button, Card, ErrorText, Input, Label, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import {
  useCreateInviteList,
  useDeleteInviteList,
  useEventTypes,
  useInviteLists,
  type InviteListSummaryDto,
} from '@/features/invitations/api';

const { colors, radius, spacing } = theme;

export default function InvitationsScreen() {
  const router = useRouter();
  const { data: lists, isLoading, isError, error, refetch, isRefetching } = useInviteLists();
  const { data: events } = useEventTypes();
  const createList = useCreateInviteList();
  const deleteList = useDeleteInviteList();

  const [showForm, setShowForm] = useState(false);
  const [groupName, setGroupName] = useState('');
  const [eventTypeId, setEventTypeId] = useState<number | null>(null);
  const [formError, setFormError] = useState('');

  // Default the picker to the first event type once it loads.
  useEffect(() => {
    if (eventTypeId == null && events && events.length > 0) setEventTypeId(events[0].id);
  }, [events, eventTypeId]);

  async function onCreate() {
    setFormError('');
    if (!groupName.trim()) {
      setFormError('Give the list a group name.');
      return;
    }
    if (eventTypeId == null) {
      setFormError('Pick an event.');
      return;
    }
    try {
      const created = await createList.mutateAsync({ groupName: groupName.trim(), eventTypeId });
      setGroupName('');
      setShowForm(false);
      router.push({ pathname: '/(app)/invite-list', params: { id: String(created.id) } });
    } catch (e) {
      setFormError(apiErrorMessage(e));
    }
  }

  function onDelete(list: InviteListSummaryDto) {
    Alert.alert('Delete this list?', `“${list.groupName}” and everyone on it will be removed.`, [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Delete', style: 'destructive', onPress: () => deleteList.mutate(list.id) },
    ]);
  }

  return (
    <Screen>
      <View style={styles.header}>
        <Text style={styles.title}>Invitations</Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={showForm ? 'Close new list form' : 'New invitation list'}
          style={styles.headerButton}
          onPress={() => setShowForm((s) => !s)}
        >
          <Ionicons name={showForm ? 'close' : 'add'} size={24} color={colors.text} />
        </Pressable>
      </View>

      <FlatList
        data={lists ?? []}
        keyExtractor={(l) => String(l.id)}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={{ height: spacing(2.5) }} />}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.primary} />
        }
        ListHeaderComponent={
          showForm ? (
            <Card style={styles.formCard}>
              <Label>Event</Label>
              {events && events.length > 0 ? (
                <View style={styles.chipRow}>
                  {events.map((e) => (
                    <Pressable
                      key={e.id}
                      style={[styles.chip, eventTypeId === e.id && styles.chipActive]}
                      onPress={() => setEventTypeId(e.id)}
                    >
                      <Text style={[styles.chipText, eventTypeId === e.id && styles.chipTextActive]}>{e.name}</Text>
                    </Pressable>
                  ))}
                </View>
              ) : (
                <Muted>Loading events…</Muted>
              )}

              <Label>Group name</Label>
              <Input
                value={groupName}
                onChangeText={setGroupName}
                placeholder="e.g. Office colleagues, College friends"
              />

              <ErrorText>{formError}</ErrorText>
              <View style={{ height: spacing(3) }} />
              <Button title="Create list" onPress={onCreate} loading={createList.isPending} />
            </Card>
          ) : null
        }
        renderItem={({ item }) => <InviteListCard list={item} onOpen={() => router.push({ pathname: '/(app)/invite-list', params: { id: String(item.id) } })} onDelete={() => onDelete(item)} />}
        ListEmptyComponent={
          <View style={styles.empty}>
            {isLoading ? (
              <Muted>Loading your invitation lists…</Muted>
            ) : isError ? (
              <Muted>{apiErrorMessage(error)}</Muted>
            ) : (
              <>
                <Ionicons name="mail-open-outline" size={40} color={colors.accent} />
                <Text style={styles.emptyTitle}>No invitation lists yet</Text>
                <Muted>Pick an event and name your group above to start.</Muted>
              </>
            )}
          </View>
        }
      />
    </Screen>
  );
}

function InviteListCard({
  list,
  onOpen,
  onDelete,
}: {
  list: InviteListSummaryDto;
  onOpen: () => void;
  onDelete: () => void;
}) {
  return (
    <Pressable style={styles.row} onPress={onOpen}>
      <View style={styles.badge}>
        <Ionicons name="mail-outline" size={20} color={colors.primary} />
      </View>
      <View style={styles.body}>
        <Text style={styles.eventName}>{list.eventName}</Text>
        <Text style={styles.groupName} numberOfLines={1}>
          {list.groupName}
        </Text>
        <View style={styles.metaRow}>
          <Text style={styles.metaText}>
            {list.invited}/{list.total} invited
          </Text>
          {list.pending > 0 ? <Text style={styles.metaPending}>· {list.pending} pending</Text> : null}
        </View>
        <View style={styles.track}>
          <View style={[styles.fill, { width: `${list.percent}%` }]} />
        </View>
      </View>
      <Pressable hitSlop={10} accessibilityLabel="Delete list" onPress={onDelete}>
        <Ionicons name="trash-outline" size={18} color={colors.muted} />
      </Pressable>
    </Pressable>
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
  chipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing(2) },
  chip: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 999,
    paddingHorizontal: spacing(3.5),
    paddingVertical: spacing(2),
    backgroundColor: colors.surface2,
  },
  chipActive: { borderColor: colors.primary, backgroundColor: colors.primary + '22' },
  chipText: { color: colors.muted, fontWeight: '600', fontSize: 13 },
  chipTextActive: { color: colors.primary },
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
  badge: {
    width: 40,
    height: 40,
    borderRadius: 20,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.primary + '18',
  },
  body: { flex: 1, gap: spacing(1) },
  eventName: { color: colors.muted, fontSize: 11, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.5 },
  groupName: { color: colors.text, fontSize: 16, fontWeight: '700' },
  metaRow: { flexDirection: 'row', alignItems: 'center', gap: spacing(1) },
  metaText: { color: colors.muted, fontSize: 12 },
  metaPending: { color: colors.accent, fontSize: 12 },
  track: { height: 6, borderRadius: 3, backgroundColor: colors.surface2, overflow: 'hidden', marginTop: spacing(1) },
  fill: { height: '100%', backgroundColor: colors.primary, borderRadius: 3 },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(20) },
  emptyTitle: { color: colors.text, fontSize: 18, fontWeight: '700' },
});
