import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useRef, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Input, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { useSendChat } from '@/features/ai/api';

const { colors, radius, spacing } = theme;

type ChatMessage = { id: string; role: 'user' | 'assistant'; text: string };

let nextMessageId = 0;
const makeId = () => String(nextMessageId++);

export default function AssistantScreen() {
  const router = useRouter();
  const sendChat = useSendChat();
  const [messages, setMessages] = useState<ChatMessage[]>([
    { id: makeId(), role: 'assistant', text: 'Tell me what you need to do, and I’ll turn it into a task.' },
  ]);
  const [input, setInput] = useState('');
  const listRef = useRef<FlatList<ChatMessage>>(null);

  function scrollToEnd() {
    requestAnimationFrame(() => listRef.current?.scrollToEnd({ animated: true }));
  }

  async function onSend() {
    const text = input.trim();
    if (!text || sendChat.isPending) return;
    setInput('');
    setMessages((prev) => [...prev, { id: makeId(), role: 'user', text }]);
    scrollToEnd();
    try {
      const { reply } = await sendChat.mutateAsync(text);
      setMessages((prev) => [...prev, { id: makeId(), role: 'assistant', text: reply }]);
    } catch (e) {
      setMessages((prev) => [...prev, { id: makeId(), role: 'assistant', text: apiErrorMessage(e) }]);
    } finally {
      scrollToEnd();
    }
  }

  return (
    <Screen>
      <View style={styles.header}>
        <Pressable hitSlop={10} onPress={() => router.back()} accessibilityLabel="Back">
          <Ionicons name="arrow-back" size={22} color={colors.text} />
        </Pressable>
        <Text style={styles.title}>Assistant</Text>
      </View>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <FlatList
          ref={listRef}
          data={messages}
          keyExtractor={(m) => m.id}
          contentContainerStyle={styles.list}
          onContentSizeChange={scrollToEnd}
          renderItem={({ item }) => (
            <View style={[styles.bubbleRow, item.role === 'user' ? styles.bubbleRowUser : styles.bubbleRowAssistant]}>
              <View style={[styles.bubble, item.role === 'user' ? styles.bubbleUser : styles.bubbleAssistant]}>
                <Text style={[styles.bubbleText, item.role === 'user' && styles.bubbleTextUser]}>{item.text}</Text>
              </View>
            </View>
          )}
          ListFooterComponent={
            sendChat.isPending ? (
              <View style={[styles.bubbleRow, styles.bubbleRowAssistant]}>
                <View style={[styles.bubble, styles.bubbleAssistant]}>
                  <ActivityIndicator color={colors.muted} size="small" />
                </View>
              </View>
            ) : null
          }
        />
        <View style={styles.inputRow}>
          <Input
            style={styles.input}
            value={input}
            onChangeText={setInput}
            placeholder="Message the assistant…"
            onSubmitEditing={onSend}
            returnKeyType="send"
            multiline
          />
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Send"
            style={[styles.sendButton, (!input.trim() || sendChat.isPending) && { opacity: 0.5 }]}
            disabled={!input.trim() || sendChat.isPending}
            onPress={onSend}
          >
            <Ionicons name="send" size={18} color="#fff" />
          </Pressable>
        </View>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(3),
    paddingHorizontal: spacing(5),
    paddingTop: spacing(3),
    paddingBottom: spacing(2),
  },
  title: { color: colors.text, fontSize: 24, fontWeight: '800' },
  list: { padding: spacing(5), paddingBottom: spacing(3), flexGrow: 1 },
  bubbleRow: { flexDirection: 'row', marginBottom: spacing(3) },
  bubbleRowUser: { justifyContent: 'flex-end' },
  bubbleRowAssistant: { justifyContent: 'flex-start' },
  bubble: { maxWidth: '82%', borderRadius: radius.md, paddingHorizontal: spacing(4), paddingVertical: spacing(3) },
  bubbleUser: { backgroundColor: colors.brand1, borderBottomRightRadius: spacing(1) },
  bubbleAssistant: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderBottomLeftRadius: spacing(1),
  },
  bubbleText: { color: colors.text, fontSize: 15, lineHeight: 21 },
  bubbleTextUser: { color: '#fff' },
  inputRow: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: spacing(2.5),
    paddingHorizontal: spacing(5),
    paddingVertical: spacing(3),
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  input: { flex: 1, minHeight: 48, maxHeight: 120, paddingTop: spacing(3) },
  sendButton: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.brand1,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
