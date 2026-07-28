import { useMutation } from '@tanstack/react-query';
import { api } from '@/lib/api/client';

export type ResolvedPlace = { lat: number; lng: number; label: string | null };

export function useResolvePlace() {
  return useMutation({
    mutationFn: async (text: string) =>
      (await api.post<ResolvedPlace>('/routeplanner/resolve', { text })).data,
  });
}

/** Great-circle distance in kilometres (mean Earth radius 6371.0088 km). Copied from
 * wwwroot/js/route-planner.js so the mobile ranking matches the web app exactly. */
export function haversineKm(a: { lat: number; lng: number }, b: { lat: number; lng: number }): number {
  const rad = (d: number) => (d * Math.PI) / 180;
  const dLat = rad(b.lat - a.lat);
  const dLng = rad(b.lng - a.lng);
  const s = Math.sin(dLat / 2) ** 2 + Math.cos(rad(a.lat)) * Math.cos(rad(b.lat)) * Math.sin(dLng / 2) ** 2;
  return 2 * 6371.0088 * Math.asin(Math.min(1, Math.sqrt(s)));
}

export function formatKm(km: number): string {
  if (km < 1) return `${Math.round(km * 1000)} m`;
  return `${km < 10 ? Math.round(km * 10) / 10 : Math.round(km)} km`;
}
