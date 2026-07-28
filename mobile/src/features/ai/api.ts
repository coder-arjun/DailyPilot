import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type ChatReply = { reply: string };

/** Sends a chat message to the AI assistant; on the server this both creates a task
 * from the parsed natural-language message and replies with a confirmation summary. */
export function useSendChat() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (message: string) => (await api.post<ChatReply>('/ai/chat', { message })).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['tasks'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['account'] });
      qc.invalidateQueries({ queryKey: ['history'] });
    },
  });
}
