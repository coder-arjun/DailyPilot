import { Link } from 'expo-router';
import React, { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Button, Card, ErrorText, Input, Label, Muted, Screen } from '@/components/ui';
import { theme } from '@/lib/theme';
import { useAuthStore } from '@/stores/authStore';

export default function LoginScreen() {
  const login = useAuthStore((s) => s.login);
  const [loginId, setLoginId] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function onSubmit() {
    setError('');
    setBusy(true);
    try {
      await login(loginId.trim(), password);
      // (auth)/_layout redirects on signedIn
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <Screen>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
          <View style={styles.brand}>
            <Text style={styles.brandTitle}>DayPilot</Text>
            <Muted>Plan. Complete. Move Forward.</Muted>
          </View>

          <Card>
            <Label>Email or username</Label>
            <Input
              value={loginId}
              onChangeText={setLoginId}
              autoCapitalize="none"
              autoCorrect={false}
              autoComplete="username"
              placeholder="your username or email"
            />
            <Label>Password</Label>
            <Input
              value={password}
              onChangeText={setPassword}
              secureTextEntry
              autoComplete="current-password"
              placeholder="Your password"
              onSubmitEditing={onSubmit}
            />
            <ErrorText>{error}</ErrorText>
            <View style={{ height: theme.spacing(5) }} />
            <Button title="Log in" onPress={onSubmit} loading={busy} />
          </Card>

          <View style={styles.footer}>
            <Muted>New to DayPilot?</Muted>
            <Link href="/(auth)/register" style={styles.link}>
              Create an account
            </Link>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: { flexGrow: 1, justifyContent: 'center', padding: theme.spacing(5) },
  brand: { alignItems: 'center', marginBottom: theme.spacing(7) },
  brandTitle: {
    color: theme.colors.text,
    fontSize: 34,
    fontWeight: '800',
    letterSpacing: -0.5,
    marginBottom: theme.spacing(1),
  },
  footer: { flexDirection: 'row', gap: 6, justifyContent: 'center', marginTop: theme.spacing(6) },
  link: { color: theme.colors.primary, fontWeight: '600' },
});
