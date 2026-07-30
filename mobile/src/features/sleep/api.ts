import axios from 'axios';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type SleepEntryDto = {
  date: string; // yyyy-MM-dd (wake-up date)
  bedTime: string; // HH:mm
  estimatedSleepTime: string; // HH:mm
  wakeTime: string | null;
  timeOutOfBed: string | null;
  quality: number | null; // 1-10
  phoneBeforeBed: boolean | null;
  dreamRemembered: boolean | null;
  notes: string | null;
  isComplete: boolean;
  /** True when bedTime was synthesized (no evening entry logged yet for this night)
   * rather than entered by the user — label it as an estimate, never as real data. */
  bedTimeEstimated: boolean | null;
};

export type SleepRecentPointDto = {
  date: string; // yyyy-MM-dd
  durationMinutes: number;
  quality: number;
};

export type SleepDashboardDto = {
  today: SleepEntryDto | null;
  todayDurationMinutes: number | null;
  todayScore: number | null;
  todayBand: string | null;
  averageDurationMinutes: number | null;
  averageBedTime: string | null;
  averageWakeTime: string | null;
  sleepDebtMinutes: number;
  consistencyPct: number | null;
  idealBedTime: string | null;
  weekendShiftMinutes: number;
  lateSleepDays: number;
  earlyWakeDays: number;
  completeNightCount: number;
  recent: SleepRecentPointDto[];
};

export type SleepInsightsDto = {
  weekly: string[];
  personalized: string[];
};

/** PUT body — `date`/`bedTime` are the only truly required fields. `estimatedSleepTime`
 * is NOT preserved when omitted: the server always (re)computes it, either from the
 * supplied value or as `bedTime + 15m` (see SleepApiController.Upsert) — same as
 * `bedTime` itself, it is never "left untouched." Every other optional field —
 * `wakeTime`, `timeOutOfBed`, `quality`, `phoneBeforeBed`, `dreamRemembered`, `notes`,
 * `bedTimeEstimated` — genuinely is left untouched server-side when omitted (undefined)
 * rather than cleared (see SleepService.UpsertAsync's conditional `is not null` writes). */
export type SleepEntryWrite = {
  date: string;
  bedTime: string;
  estimatedSleepTime?: string | null;
  wakeTime?: string | null;
  timeOutOfBed?: string | null;
  quality?: number | null;
  phoneBeforeBed?: boolean | null;
  dreamRemembered?: boolean | null;
  notes?: string | null;
  /** True when `bedTime` above was synthesized rather than user-entered. Pass `false`
   * explicitly whenever the user saved a real evening entry; omit to leave unchanged. */
  bedTimeEstimated?: boolean | null;
};

const keys = {
  dashboard: ['sleep', 'dashboard'] as const,
  insights: ['sleep', 'insights'] as const,
  entry: (date: string) => ['sleep', 'entry', date] as const,
};

export function useSleepDashboard() {
  return useQuery({
    queryKey: keys.dashboard,
    queryFn: async () => (await api.get<SleepDashboardDto>('/sleep/dashboard')).data,
  });
}

export function useSleepInsights() {
  return useQuery({
    queryKey: keys.insights,
    queryFn: async () => (await api.get<SleepInsightsDto>('/sleep/insights')).data,
  });
}

/** Fetches the entry for a given wake-up date; resolves to `null` (not an error)
 * when nothing has been logged for that date yet, so screens can prefill-or-blank. */
export function useSleepEntry(date: string) {
  return useQuery({
    queryKey: keys.entry(date),
    queryFn: async () => {
      try {
        const { data } = await api.get<SleepEntryDto>('/sleep/entry', { params: { date } });
        return data;
      } catch (e) {
        if (axios.isAxiosError(e) && e.response?.status === 404) return null;
        throw e;
      }
    },
    enabled: !!date,
  });
}

export function useUpsertSleepEntry() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: SleepEntryWrite) => (await api.put<SleepEntryDto>('/sleep/entry', body)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sleep'] }),
  });
}
