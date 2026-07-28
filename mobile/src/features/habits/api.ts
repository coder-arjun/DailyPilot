import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type HabitDto = {
  id: number;
  name: string;
  description: string | null;
  color: string;
  frequency: number; // 0 Daily, 1 Weekly
  targetPerWeek: number;
  currentStreak: number;
  longestStreak: number;
  thisWeekCount: number;
  checkedToday: boolean;
};

export type HabitWrite = {
  name: string;
  description?: string | null;
  frequency: number;
  targetPerWeek: number;
  color?: string | null;
};

const keys = {
  habits: ['habits'] as const,
};

/** Invalidates habit queries plus everything that surfaces habit-derived data
 * (dashboard stats, account/streak info, history log) so they don't go stale. */
function invalidateHabitsAndRelated(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: keys.habits });
  qc.invalidateQueries({ queryKey: ['dashboard'] });
  qc.invalidateQueries({ queryKey: ['account'] });
  qc.invalidateQueries({ queryKey: ['history'] });
}

export function useHabits() {
  return useQuery({
    queryKey: keys.habits,
    queryFn: async () => (await api.get<HabitDto[]>('/habits')).data,
  });
}

export function useCreateHabit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: HabitWrite) => (await api.post<HabitDto>('/habits', body)).data,
    onSuccess: () => invalidateHabitsAndRelated(qc),
  });
}

/** Optimistic today-checkin toggle: flips the row in cache, rolls back on error. */
export function useToggleHabitCheckin() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (habit: HabitDto) =>
      api.post(`/habits/${habit.id}/${habit.checkedToday ? 'uncheck' : 'checkin'}`, {}),
    onMutate: async (habit) => {
      await qc.cancelQueries({ queryKey: keys.habits });
      const previous = qc.getQueryData<HabitDto[]>(keys.habits);
      qc.setQueryData<HabitDto[]>(keys.habits, (old) =>
        old?.map((h) => (h.id === habit.id ? { ...h, checkedToday: !h.checkedToday } : h)),
      );
      return { previous };
    },
    onError: (_err, _habit, ctx) => {
      if (ctx?.previous) qc.setQueryData(keys.habits, ctx.previous);
    },
    onSettled: () => invalidateHabitsAndRelated(qc),
  });
}

export const habitColorPresets = ['#198754', '#6366F1', '#F59E0B', '#f87171', '#22c55e', '#9333EA'];
