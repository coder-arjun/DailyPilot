import { Link } from 'expo-router';
import React, { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Button, Card, ErrorText, Input, Label, Muted, Screen } from '@/components/ui';
import { theme } from '@/lib/theme';
import { useAuthStore, type RegisterFields } from '@/stores/authStore';

export default function RegisterScreen() {
  const register = useAuthStore((s) => s.register);
  const [displayName, setDisplayName] = useState('');
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function onSubmit() {
    setError('');
    if (password !== confirm) {
      setError('The password and confirmation do not match.');
      return;
    }
    setBusy(true);
    try {
      const fields: RegisterFields = {
        displayName: displayName.trim(),
        userName: userName.trim(),
        email: email.trim(),
        timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'UTC',
        password,
      };
      await register(fields);
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
          <Text style={styles.title}>Create your account</Text>

          <Card>
            <Label>Display name</Label>
            <Input value={displayName} onChangeText={setDisplayName} placeholder="How should we call you?" />
            <Label>Username</Label>
            <Input
              value={userName}
              onChangeText={setUserName}
              autoCapitalize="none"
              autoCorrect={false}
              placeholder="letters, numbers and . _ - @ +"
            />
            <Label>Email</Label>
            <Input
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              keyboardType="email-address"
              autoComplete="email"
              placeholder="you@example.com"
            />
            <Label>Password</Label>
            <Input value={password} onChangeText={setPassword} secureTextEntry placeholder="At least 6 characters" />
            <Label>Confirm password</Label>
            <Input value={confirm} onChangeText={setConfirm} secureTextEntry placeholder="Repeat the password" />
            <ErrorText>{error}</ErrorText>
            <View style={{ height: theme.spacing(5) }} />
            <Button title="Create account" onPress={onSubmit} loading={busy} />
          </Card>

          <View style={styles.footer}>
            <Muted>Already have an account?</Muted>
            <Link href="/(auth)/login" style={styles.link}>
              Log in
            </Link>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: { flexGrow: 1, justifyContent: 'center', padding: theme.spacing(5) },
  title: {
    color: theme.colors.text,
    fontSize: 26,
    fontWeight: '800',
    marginBottom: theme.spacing(5),
    textAlign: 'center',
  },
  footer: { flexDirection: 'row', gap: 6, justifyContent: 'center', marginTop: theme.spacing(6) },
  link: { color: theme.colors.primary, fontWeight: '600' },
});
