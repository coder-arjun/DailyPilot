import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams, useRouter } from 'expo-router';
import React, { useState } from 'react';
import { Alert, FlatList, Linking, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { Button, Card, ErrorText, Input, Label, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import {
  useAddInvitee,
  useDeleteInvitee,
  useInviteList,
  useToggleInvitee,
  whatsAppUrlFor,
  type InviteeDto,
} from '@/features/invitations/api';

const { colors, radius, spacing } = theme;

export default function InviteListScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ id?: string }>();
  const listId = params.id ? Number(params.id) : null;

  const { data: list, isLoading, isError, error, refetch, isRefetching } = useInviteList(listId);
  const addInvitee = useAddInvitee(listId ?? 0);
  const toggleInvitee = useToggleInvitee(listId ?? 0);
  const deleteInvitee = useDeleteInvitee(listId ?? 0);

  const [name, setName] = useState('');
  const [contact, setContact] = useState('');
  const [formError, setFormError] = useState('');

  async function onAdd() {
    setFormError('');
    if (!name.trim()) {
      setFormError('Enter a name.');
      return;
    }
    try {
      await addInvitee.mutateAsync({ name: name.trim(), contact: contact.trim() || null });
      setName('');
      setContact('');
    } catch (e) {
      setFormError(apiErrorMessage(e));
    }
  }

  function onRemove(invitee: InviteeDto) {
    Alert.alert('Remove this person?', `Remove ${invitee.name} from the list.`, [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Remove', style: 'destructive', onPress: () => deleteInvitee.mutate(invitee.id) },
    ]);
  }

  function onShare(invitee: InviteeDto) {
    Linking.openURL(whatsAppUrlFor(invitee)).catch(() => {
      Alert.alert('Couldn’t open WhatsApp', 'Make sure WhatsApp is installed on this device.');
    });
  }

  return (
    <Screen>
      <View style={styles.header}>
        <Pressable hitSlop={10} onPress={() => router.back()} accessibilityLabel="Back">
          <Ionicons name="arrow-back" size={22} color={colors.text} />
        </Pressable>
        <View style={{ flex: 1 }}>
          <Text style={styles.title} numberOfLines={1}>
            {list?.groupName ?? 'Invitation list'}
          </Text>
          {list ? <Muted>{list.eventName}</Muted> : null}
        </View>
      </View>

      <FlatList
        data={list?.invitees ?? []}
        keyExtractor={(i) => String(i.id)}
        contentContainerStyle={styles.list}
        ItemSeparatorComponent={() => <View style={{ height: spacing(2.5) }} />}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.primary} />
        }
        ListHeaderComponent={
          list ? (
            <>
              <Card style={styles.summaryCard}>
                <View style={styles.summaryRow}>
                  <Text style={styles.summaryText}>
                    <Text style={styles.summaryStrong}>{list.invited}</Text> of{' '}
                    <Text style={styles.summaryStrong}>{list.total}</Text> invited
                  </Text>
                  {list.pending > 0 ? <Muted>{list.pending} still to invite</Muted> : null}
                </View>
                <View style={styles.track}>
                  <View style={[styles.fill, { width: `${list.percent}%` }]} />
                </View>
              </Card>

              <Card style={styles.formCard}>
                <Label>Add a person</Label>
                <Input value={name} onChangeText={setName} placeholder="Full name" />
                <Label>Phone / email (optional)</Label>
                <Input value={contact} onChangeText={setContact} placeholder="Optional" />
                <ErrorText>{formError}</ErrorText>
                <View style={{ height: spacing(3) }} />
                <Button title="Add" onPress={onAdd} loading={addInvitee.isPending} />
              </Card>
            </>
          ) : null
        }
        renderItem={({ item }) => (
          <InviteeRow
            invitee={item}
            onToggle={() => toggleInvitee.mutate(item)}
            onShare={() => onShare(item)}
            onRemove={() => onRemove(item)}
          />
        )}
        ListEmptyComponent={
          <View style={styles.empty}>
            {isLoading ? (
              <Muted>Loading…</Muted>
            ) : isError ? (
              <Muted>{apiErrorMessage(error)}</Muted>
            ) : list ? (
              <>
                <Ionicons name="person-add-outline" size={40} color={colors.accent} />
                <Text style={styles.emptyTitle}>No one on this list yet</Text>
                <Muted>Add everyone above so you don't miss anybody.</Muted>
              </>
            ) : null}
          </View>
        }
      />
    </Screen>
  );
}

function InviteeRow({
  invitee,
  onToggle,
  onShare,
  onRemove,
}: {
  invitee: InviteeDto;
  onToggle: () => void;
  onShare: () => void;
  onRemove: () => void;
}) {
  return (
    <View style={styles.row}>
      <View style={styles.body}>
        <Text style={styles.name}>{invitee.name}</Text>
        <Muted>{invitee.contact || '—'}</Muted>
      </View>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="WhatsApp this person"
        hitSlop={8}
        style={styles.iconButton}
        onPress={onShare}
      >
        <Ionicons name="logo-whatsapp" size={20} color={colors.success} />
      </Pressable>
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ checked: invitee.isInvited }}
        onPress={onToggle}
        style={[styles.pill, invitee.isInvited ? styles.pillInvited : styles.pillPending]}
      >
        <Ionicons
          name={invitee.isInvited ? 'checkmark-circle' : 'hourglass-outline'}
          size={14}
          color={invitee.isInvited ? colors.success : colors.accent}
        />
        <Text style={[styles.pillText, { color: invitee.isInvited ? colors.success : colors.accent }]}>
          {invitee.isInvited ? 'Invited' : 'Pending'}
        </Text>
      </Pressable>
      <Pressable hitSlop={8} accessibilityLabel={`Remove ${invitee.name}`} onPress={onRemove}>
        <Ionicons name="close" size={18} color={colors.muted} />
      </Pressable>
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
  title: { color: colors.text, fontSize: 22, fontWeight: '800' },
  list: { paddingHorizontal: spacing(5), paddingBottom: spacing(24), flexGrow: 1 },
  summaryCard: { marginBottom: spacing(3) },
  summaryRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing(2) },
  summaryText: { color: colors.text, fontSize: 14 },
  summaryStrong: { fontWeight: '700' },
  track: { height: 8, borderRadius: 4, backgroundColor: colors.surface2, overflow: 'hidden' },
  fill: { height: '100%', backgroundColor: colors.primary, borderRadius: 4 },
  formCard: { marginBottom: spacing(4) },
  row: {
    flexDirection: 'row',
    gap: spacing(2.5),
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.sm,
    padding: spacing(3.5),
  },
  body: { flex: 1, gap: spacing(1) },
  name: { color: colors.text, fontSize: 15, fontWeight: '600' },
  iconButton: { padding: spacing(1) },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(1),
    borderRadius: 999,
    paddingHorizontal: spacing(2.5),
    paddingVertical: spacing(1.5),
    borderWidth: 1,
  },
  pillInvited: { borderColor: colors.success, backgroundColor: colors.success + '18' },
  pillPending: { borderColor: colors.accent, backgroundColor: colors.accent + '18' },
  pillText: { fontSize: 11, fontWeight: '700' },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(16) },
  emptyTitle: { color: colors.text, fontSize: 18, fontWeight: '700' },
});
