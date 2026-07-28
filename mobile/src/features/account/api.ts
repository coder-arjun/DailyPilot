import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api/client';
import { useAuthStore } from '@/stores/authStore';

export type LevelInfoDto = {
  level: number;
  title: string;
  xp: number;
  xpIntoLevel: number;
  xpSpan: number;
  xpToNext: number;
  progressPct: number;
};

export type AccountSettingsDto = {
  enableMorningReminder: boolean;
  morningReminderTime: string; // HH:mm
  enableEndOfDayReminder: boolean;
  endOfDayReminderTime: string; // HH:mm
  enableAutoCarryForward: boolean;
  speakReminders: boolean;
};

export type AccountDto = {
  id: string;
  userName: string | null;
  displayName: string | null;
  email: string | null;
  timeZoneId: string;
  xp: number;
  level: LevelInfoDto;
  currentStreak: number;
  longestStreak: number;
  settings: AccountSettingsDto;
};

/** Partial body for `PUT /account/settings` — omit fields you don't want to change. */
export type AccountSettingsWrite = Partial<{
  displayName: string;
  timeZoneId: string;
  enableMorningReminder: boolean;
  morningReminderTime: string;
  enableEndOfDayReminder: boolean;
  endOfDayReminderTime: string;
  enableAutoCarryForward: boolean;
  speakReminders: boolean;
}>;

const keys = {
  account: ['account'] as const,
};

export function useAccount() {
  return useQuery({
    queryKey: keys.account,
    queryFn: async () => (await api.get<AccountDto>('/account')).data,
  });
}

export function useUpdateSettings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: AccountSettingsWrite) =>
      (await api.put<AccountDto>('/account/settings', body)).data,
    onSuccess: (data, variables) => {
      qc.setQueryData(keys.account, data);
      // Keep the auth store's user in sync so the display name updates anywhere it's
      // read from (e.g. the Today greeting) without waiting on a refetch.
      if (variables.displayName !== undefined && data.displayName) {
        useAuthStore.getState().setDisplayName(data.displayName);
      }
    },
  });
}
