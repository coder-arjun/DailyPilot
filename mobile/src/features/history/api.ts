import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type TaskHistoryDto = {
  id: number;
  taskItemId: number;
  action: string;
  details: string | null;
  taskTitle: string;
  onDate: string; // yyyy-MM-dd
  timestamp: string; // ISO 8601
};

export type HistoryPageDto = {
  items: TaskHistoryDto[];
  page: number;
  totalPages: number;
};

export function useHistory(page: number) {
  return useQuery({
    queryKey: ['history', page] as const,
    queryFn: async () => (await api.get<HistoryPageDto>('/history', { params: { page } })).data,
    placeholderData: (previous) => previous,
  });
}
