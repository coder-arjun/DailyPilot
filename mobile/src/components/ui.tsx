import React from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type PressableProps,
  type TextInputProps,
  type ViewProps,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { theme } from '@/lib/theme';

const { colors, radius, spacing } = theme;

export function Screen({ children, style, ...rest }: ViewProps) {
  return (
    <SafeAreaView style={[styles.screen, style]} {...rest}>
      {children}
    </SafeAreaView>
  );
}

export function Card({ children, style, ...rest }: ViewProps) {
  return (
    <View style={[styles.card, style]} {...rest}>
      {children}
    </View>
  );
}

type ButtonProps = PressableProps & {
  title: string;
  variant?: 'primary' | 'ghost' | 'danger';
  loading?: boolean;
};

export function Button({ title, variant = 'primary', loading, disabled, style, ...rest }: ButtonProps) {
  const isDisabled = disabled || loading;
  return (
    <Pressable
      accessibilityRole="button"
      disabled={isDisabled}
      style={({ pressed }) => [
        styles.button,
        variant === 'primary' && styles.buttonPrimary,
        variant === 'ghost' && styles.buttonGhost,
        variant === 'danger' && styles.buttonDanger,
        (pressed || isDisabled) && { opacity: 0.7 },
        typeof style === 'object' ? style : null,
      ]}
      {...rest}
    >
      {loading ? (
        <ActivityIndicator color={variant === 'ghost' ? colors.primary : '#fff'} />
      ) : (
        <Text style={[styles.buttonText, variant === 'ghost' && { color: colors.primary }]}>{title}</Text>
      )}
    </Pressable>
  );
}

export function Label({ children }: { children: React.ReactNode }) {
  return <Text style={styles.label}>{children}</Text>;
}

export const Input = React.forwardRef<TextInput, TextInputProps>(function Input({ style, ...rest }, ref) {
  return (
    <TextInput
      ref={ref}
      placeholderTextColor={colors.muted}
      style={[styles.input, style]}
      {...rest}
    />
  );
});

export function ErrorText({ children }: { children: React.ReactNode }) {
  if (!children) return null;
  return <Text style={styles.error}>{children}</Text>;
}

export function Muted({ children }: { children: React.ReactNode }) {
  return <Text style={styles.muted}>{children}</Text>;
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.bg },
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.md,
    padding: spacing(4),
  },
  button: {
    height: 48,
    borderRadius: 999,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: spacing(5),
  },
  buttonPrimary: { backgroundColor: colors.brand1 },
  buttonGhost: { backgroundColor: 'transparent', borderWidth: 1, borderColor: colors.border },
  buttonDanger: { backgroundColor: colors.danger },
  buttonText: { color: '#fff', fontWeight: '700', fontSize: 16 },
  label: { color: colors.muted, fontSize: 13, fontWeight: '600', marginBottom: spacing(1.5), marginTop: spacing(3) },
  input: {
    height: 48,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface2,
    color: colors.text,
    paddingHorizontal: spacing(3.5),
    fontSize: 16,
  },
  error: { color: colors.danger, marginTop: spacing(3), fontSize: 14 },
  muted: { color: colors.muted, fontSize: 14 },
});
