import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type InviteListSummaryDto = {
  id: number;
  groupName: string;
  eventName: string;
  eventIcon: string;
  total: number;
  invited: number;
  pending: number;
  percent: number;
  createdAt: string;
};

export type InviteeDto = {
  id: number;
  name: string;
  contact: string | null;
  isInvited: boolean;
  invitedAt: string | null;
  createdAt: string;
  /** WhatsApp invite message for this person — build the wa.me / api.whatsapp.com URL from it. */
  waShareText: string;
};

export type InviteListDetailDto = {
  id: number;
  groupName: string;
  eventName: string;
  eventIcon: string;
  notes: string | null;
  createdAt: string;
  total: number;
  invited: number;
  pending: number;
  percent: number;
  invitees: InviteeDto[];
};

export type EventTypeDto = { id: number; name: string; icon: string };

export type InviteListCreate = { groupName: string; eventTypeId: number };
export type InviteeCreate = { name: string; contact?: string | null };

const keys = {
  lists: ['invitations'] as const,
  detail: (id: number) => ['invitations', 'detail', id] as const,
  events: ['invitations', 'events'] as const,
};

export function useInviteLists() {
  return useQuery({
    queryKey: keys.lists,
    queryFn: async () => (await api.get<InviteListSummaryDto[]>('/invitations')).data,
  });
}

export function useEventTypes() {
  return useQuery({
    queryKey: keys.events,
    queryFn: async () => (await api.get<EventTypeDto[]>('/invitations/events')).data,
    staleTime: 5 * 60_000,
  });
}

export function useInviteList(id: number | null) {
  return useQuery({
    queryKey: keys.detail(id ?? 0),
    queryFn: async () => (await api.get<InviteListDetailDto>(`/invitations/${id}`)).data,
    enabled: id != null,
  });
}

export function useCreateInviteList() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: InviteListCreate) => (await api.post<InviteListDetailDto>('/invitations', body)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.lists }),
  });
}

export function useDeleteInviteList() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`/invitations/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.lists }),
  });
}

export function useAddInvitee(listId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: InviteeCreate) =>
      (await api.post<InviteeDto>(`/invitations/${listId}/invitees`, body)).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.detail(listId) });
      qc.invalidateQueries({ queryKey: keys.lists });
    },
  });
}

/** Optimistic invited/pending toggle: flips the row in the detail cache, rolls back on error. */
export function useToggleInvitee(listId: number) {
  const qc = useQueryClient();
  const key = keys.detail(listId);
  return useMutation({
    mutationFn: async (invitee: InviteeDto) =>
      (await api.post<InviteeDto>(`/invitations/invitees/${invitee.id}/toggle`, {})).data,
    onMutate: async (invitee) => {
      await qc.cancelQueries({ queryKey: key });
      const previous = qc.getQueryData<InviteListDetailDto>(key);
      qc.setQueryData<InviteListDetailDto>(key, (old) => {
        if (!old) return old;
        const invited = old.invited + (invitee.isInvited ? -1 : 1);
        return {
          ...old,
          invited,
          pending: old.total - invited,
          percent: old.total === 0 ? 0 : Math.round((invited * 100) / old.total),
          invitees: old.invitees.map((i) => (i.id === invitee.id ? { ...i, isInvited: !i.isInvited } : i)),
        };
      });
      return { previous };
    },
    onError: (_err, _invitee, ctx) => {
      if (ctx?.previous) qc.setQueryData(key, ctx.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: key });
      qc.invalidateQueries({ queryKey: keys.lists });
    },
  });
}

export function useDeleteInvitee(listId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`/invitations/invitees/${id}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.detail(listId) });
      qc.invalidateQueries({ queryKey: keys.lists });
    },
  });
}

/** Builds the WhatsApp URL for an invitee: a phone-looking Contact (7+ digits) goes straight
 * to that chat via wa.me; otherwise fall back to WhatsApp's own recipient-less contact picker. */
export function whatsAppUrlFor(invitee: InviteeDto): string {
  const digits = (invitee.contact ?? '').replace(/[^\d]/g, '');
  const text = encodeURIComponent(invitee.waShareText);
  return digits.length >= 7 ? `https://wa.me/${digits}?text=${text}` : `https://api.whatsapp.com/send?text=${text}`;
}
