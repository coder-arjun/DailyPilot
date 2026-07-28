import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type SearchTaskDto = { id: number; title: string; plannedDate: string; isCompleted: boolean };
export type SearchHabitDto = { id: number; name: string };
export type SearchCategoryDto = { id: number; name: string; color: string };
export type SearchTagDto = { id: number; name: string };
export type SearchWorkspaceDto = { id: number; name: string };

export type SearchResultsDto = {
  tasks: SearchTaskDto[];
  habits: SearchHabitDto[];
  categories: SearchCategoryDto[];
  tags: SearchTagDto[];
  workspaces: SearchWorkspaceDto[];
};

/** Debounced by the caller — this hook just gates on q.length so short queries don't fire. */
export function useSearch(q: string) {
  const query = q.trim();
  return useQuery({
    queryKey: ['search', query] as const,
    queryFn: async () => (await api.get<SearchResultsDto>('/search', { params: { q: query } })).data,
    enabled: query.length >= 2,
  });
}
