import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type TaskDto = {
  id: number;
  title: string;
  notes: string | null;
  plannedDate: string; // yyyy-MM-dd
  dueTime: string | null; // HH:mm
  reminderTime: string | null;
  priority: number; // 0 Low, 1 Medium, 2 High
  status: number;
  categoryId: number | null;
  categoryName: string | null;
  categoryColor: string | null;
  carryForwardCount: number;
  estimatedMinutes: number | null;
  actualMinutes: number | null;
  isCompleted: boolean;
};

export type CategoryDto = { id: number; name: string; color: string };

export type TaskWrite = {
  title: string;
  notes?: string | null;
  plannedDate: string;
  dueTime?: string | null;
  reminderTime?: string | null;
  priority: number;
  categoryId?: number | null;
  estimatedMinutes?: number | null;
};

const keys = {
  tasks: (date?: string) => ['tasks', date ?? 'today'] as const,
  categories: ['categories'] as const,
};

export function useTodayTasks(date?: string) {
  return useQuery({
    queryKey: keys.tasks(date),
    queryFn: async () => {
      const { data } = await api.get<TaskDto[]>('/tasks', { params: date ? { date } : {} });
      return data;
    },
  });
}

/** Fetches a single task by id, regardless of which day it's planned for — used when a
 * task isn't in the today cache (e.g. opened from search). */
export function useTask(id: number | null) {
  return useQuery({
    queryKey: ['tasks', 'byId', id] as const,
    queryFn: async () => (await api.get<TaskDto>(`/tasks/${id}`)).data,
    enabled: id != null,
  });
}

export function useCategories() {
  return useQuery({
    queryKey: keys.categories,
    queryFn: async () => (await api.get<CategoryDto[]>('/categories')).data,
    staleTime: 5 * 60_000,
  });
}

/** Invalidates task queries plus everything that surfaces task-derived data
 * (dashboard stats, account/streak info, history log) so they don't go stale. */
function invalidateTasksAndRelated(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['tasks'] });
  qc.invalidateQueries({ queryKey: ['dashboard'] });
  qc.invalidateQueries({ queryKey: ['account'] });
  qc.invalidateQueries({ queryKey: ['history'] });
}

function useInvalidateTasks() {
  const qc = useQueryClient();
  return () => invalidateTasksAndRelated(qc);
}

export function useCreateTask() {
  const invalidate = useInvalidateTasks();
  return useMutation({
    mutationFn: async (body: TaskWrite) => (await api.post<TaskDto>('/tasks', body)).data,
    onSuccess: invalidate,
  });
}

export function useUpdateTask() {
  const invalidate = useInvalidateTasks();
  return useMutation({
    mutationFn: async ({ id, ...body }: TaskWrite & { id: number }) =>
      (await api.put<TaskDto>(`/tasks/${id}`, body)).data,
    onSuccess: invalidate,
  });
}

export function useDeleteTask() {
  const invalidate = useInvalidateTasks();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`/tasks/${id}`),
    onSuccess: invalidate,
  });
}

/** Optimistic complete/reopen toggle: flips the row in cache, rolls back on error. */
export function useToggleComplete(date?: string) {
  const qc = useQueryClient();
  const key = keys.tasks(date);
  return useMutation({
    mutationFn: async (task: TaskDto) =>
      api.post(`/tasks/${task.id}/${task.isCompleted ? 'reopen' : 'complete'}`, {}),
    onMutate: async (task) => {
      await qc.cancelQueries({ queryKey: key });
      const previous = qc.getQueryData<TaskDto[]>(key);
      qc.setQueryData<TaskDto[]>(key, (old) =>
        old?.map((t) => (t.id === task.id ? { ...t, isCompleted: !t.isCompleted } : t)),
      );
      return { previous };
    },
    onError: (_err, _task, ctx) => {
      if (ctx?.previous) qc.setQueryData(key, ctx.previous);
    },
    onSettled: () => invalidateTasksAndRelated(qc),
  });
}

export const priorityMeta: Record<number, { label: string; color: string }> = {
  0: { label: 'Low', color: '#94a3b8' },
  1: { label: 'Medium', color: '#818cf8' },
  2: { label: 'High', color: '#F59E0B' },
};
