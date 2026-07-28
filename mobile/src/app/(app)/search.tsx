import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { Input, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { useSearch } from '@/features/search/api';

const { colors, radius, spacing } = theme;

export default function SearchScreen() {
  const router = useRouter();
  const inputRef = useRef<TextInput>(null);
  const [text, setText] = useState('');
  const [debounced, setDebounced] = useState('');

  useEffect(() => {
    const t = setTimeout(() => setDebounced(text), 300);
    return () => clearTimeout(t);
  }, [text]);

  const { data, isLoading, isError, error } = useSearch(debounced);

  const query = debounced.trim();
  const hasQuery = query.length >= 2;
  const totalResults = data
    ? data.tasks.length + data.habits.length + data.categories.length + data.tags.length + data.workspaces.length
    : 0;

  return (
    <Screen>
      <View style={styles.header}>
        <Text style={styles.title}>Search</Text>
      </View>

      <View style={styles.searchBar}>
        <Ionicons name="search" size={18} color={colors.muted} style={styles.searchIcon} />
        <Input
          ref={inputRef}
          value={text}
          onChangeText={setText}
          placeholder="Search tasks, habits, categories…"
          style={styles.searchInput}
          autoFocus
          returnKeyType="search"
        />
      </View>

      <ScrollView contentContainerStyle={styles.list} keyboardShouldPersistTaps="handled">
        {!hasQuery ? (
          <View style={styles.empty}>
            <Ionicons name="search-outline" size={40} color={colors.accent} />
            <Muted>Type at least 2 characters to search.</Muted>
          </View>
        ) : isLoading ? (
          <View style={styles.empty}>
            <Muted>Searching…</Muted>
          </View>
        ) : isError ? (
          <View style={styles.empty}>
            <Muted>{apiErrorMessage(error)}</Muted>
          </View>
        ) : !data || totalResults === 0 ? (
          <View style={styles.empty}>
            <Ionicons name="file-tray-outline" size={40} color={colors.muted} />
            <Text style={styles.emptyTitle}>No matches</Text>
            <Muted>Try a different search term.</Muted>
          </View>
        ) : (
          <>
            {data.tasks.length > 0 ? (
              <Section title="Tasks">
                {data.tasks.map((t) => (
                  <Pressable
                    key={t.id}
                    style={({ pressed }) => [styles.row, pressed && { opacity: 0.8 }]}
                    onPress={() => router.push({ pathname: '/(app)/task-editor', params: { id: String(t.id) } })}
                  >
                    <Ionicons
                      name={t.isCompleted ? 'checkmark-circle' : 'ellipse-outline'}
                      size={20}
                      color={t.isCompleted ? colors.success : colors.muted}
                    />
                    <View style={styles.rowBody}>
                      <Text style={styles.rowTitle} numberOfLines={1}>
                        {t.title}
                      </Text>
                      <Muted>{t.plannedDate}</Muted>
                    </View>
                  </Pressable>
                ))}
              </Section>
            ) : null}

            {data.habits.length > 0 ? (
              <Section title="Habits">
                <View style={styles.chipRow}>
                  {data.habits.map((h) => (
                    <View key={h.id} style={styles.chip}>
                      <Text style={styles.chipText}>{h.name}</Text>
                    </View>
                  ))}
                </View>
              </Section>
            ) : null}

            {data.categories.length > 0 ? (
              <Section title="Categories">
                <View style={styles.chipRow}>
                  {data.categories.map((c) => (
                    <View key={c.id} style={[styles.chip, { backgroundColor: `${c.color}33`, borderColor: c.color }]}>
                      <Text style={[styles.chipText, { color: c.color }]}>{c.name}</Text>
                    </View>
                  ))}
                </View>
              </Section>
            ) : null}

            {data.tags.length > 0 ? (
              <Section title="Tags">
                <View style={styles.chipRow}>
                  {data.tags.map((t) => (
                    <View key={t.id} style={styles.chip}>
                      <Text style={styles.chipText}>{t.name}</Text>
                    </View>
                  ))}
                </View>
              </Section>
            ) : null}

            {data.workspaces.length > 0 ? (
              <Section title="Workspaces">
                <View style={styles.chipRow}>
                  {data.workspaces.map((w) => (
                    <View key={w.id} style={styles.chip}>
                      <Text style={styles.chipText}>{w.name}</Text>
                    </View>
                  ))}
                </View>
              </Section>
            ) : null}
          </>
        )}
      </ScrollView>
    </Screen>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{title}</Text>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  header: {
    paddingHorizontal: spacing(5),
    paddingTop: spacing(3),
    paddingBottom: spacing(2),
  },
  title: { color: colors.text, fontSize: 24, fontWeight: '800' },
  searchBar: { marginHorizontal: spacing(5), marginBottom: spacing(3) },
  searchIcon: { position: 'absolute', left: spacing(3.5), top: 15, zIndex: 1 },
  searchInput: { paddingLeft: spacing(9) },
  list: { paddingHorizontal: spacing(5), paddingBottom: spacing(24), flexGrow: 1, gap: spacing(4) },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing(2), paddingTop: spacing(16) },
  emptyTitle: { color: colors.text, fontSize: 18, fontWeight: '700' },
  section: { gap: spacing(2.5) },
  sectionTitle: { color: colors.muted, fontSize: 13, fontWeight: '700', textTransform: 'uppercase' },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(3),
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.sm,
    padding: spacing(3.5),
    marginBottom: spacing(2),
  },
  rowBody: { flex: 1, gap: spacing(1) },
  rowTitle: { color: colors.text, fontSize: 15, fontWeight: '600' },
  chipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing(2) },
  chip: {
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface2,
    borderRadius: 999,
    paddingHorizontal: spacing(3.5),
    paddingVertical: spacing(2),
  },
  chipText: { color: colors.text, fontSize: 13, fontWeight: '600' },
});
