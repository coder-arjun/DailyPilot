import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type LevelInfoDto = {
  level: number;
  title: string;
  xp: number;
  xpIntoLevel: number;
  xpSpan: number;
  xpToNext: number;
  progressPct: number;
};

export type DashboardTrendPointDto = {
  date: string; // yyyy-MM-dd
  completed: number;
  total: number;
};

export type DashboardDto = {
  displayName: string;
  level: LevelInfoDto;
  todayCompleted: number;
  todayTotal: number;
  currentStreak: number;
  longestStreak: number;
  trend: DashboardTrendPointDto[];
};

export function useDashboard() {
  return useQuery({
    queryKey: ['dashboard'] as const,
    queryFn: async () => (await api.get<DashboardDto>('/dashboard')).data,
  });
}
