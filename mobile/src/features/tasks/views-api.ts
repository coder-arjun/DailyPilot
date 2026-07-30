import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api/client';
import { playCompletionTone } from '@/lib/sounds';
import type { TaskDto } from './api';

export type CalendarDayDto = { date: string; total: number; completed: number };

export type CalendarMonthDto = { days: CalendarDayDto[]; tasks: TaskDto[] };

export type BoardDto = {
  pending: TaskDto[];
  inProgress: TaskDto[];
  completed: TaskDto[];
  carriedForward: TaskDto[];
};

/** `month` is `yyyy-MM`. Returns day counts + every task planned in that month. */
export function useCalendarMonth(month: string) {
  return useQuery({
    queryKey: ['tasks', 'calendar', month] as const,
    queryFn: async () => (await api.get<CalendarMonthDto>('/tasks/calendar', { params: { month } })).data,
  });
}

/** Local-today tasks grouped by lifecycle status, for the Kanban board. */
export function useBoard() {
  return useQuery({
    queryKey: ['tasks', 'board'] as const,
    queryFn: async () => (await api.get<BoardDto>('/tasks/board')).data,
  });
}

/** Moves a task to a new lifecycle status; invalidates all task queries (today/calendar/board). */
export function useSetStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, status }: { id: number; status: number }) =>
      (await api.post<TaskDto>(`/tasks/${id}/status`, { status })).data,
    onMutate: ({ status }) => {
      if (status === 1) playCompletionTone(); // moving into Completed
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['tasks'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['account'] });
      qc.invalidateQueries({ queryKey: ['history'] });
    },
  });
}
