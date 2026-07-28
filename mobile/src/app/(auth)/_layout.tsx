import { Redirect, Stack } from 'expo-router';
import React from 'react';
import { theme } from '@/lib/theme';
import { useAuthStore } from '@/stores/authStore';

export default function AuthLayout() {
  const status = useAuthStore((s) => s.status);
  if (status === 'signedIn') return <Redirect href="/(app)" />;

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.colors.bg },
      }}
    />
  );
}
