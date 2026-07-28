import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { config } from '@/lib/config';
import { useAuthStore } from '@/stores/authStore';

/**
 * Authenticated API client. Injects the current access token; on a 401 it
 * refreshes once (single-flight via the auth store) and replays the request.
 */
export const api = axios.create({
  baseURL: config.apiUrl,
  headers: { 'Content-Type': 'application/json', 'X-Client': 'mobile' },
  timeout: 20000,
});

api.interceptors.request.use((req) => {
  const token = useAuthStore.getState().accessToken;
  if (token) req.headers.Authorization = `Bearer ${token}`;
  return req;
});

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

api.interceptors.response.use(undefined, async (error: AxiosError) => {
  const original = error.config as RetriableConfig | undefined;
  if (error.response?.status === 401 && original && !original._retried) {
    original._retried = true;
    await useAuthStore.getState().refresh(); // throws (and signs out) when the refresh token is dead
    return api(original);
  }
  throw error;
});

/** Server error contract is {"error": "..."} — extract it for display. */
export function apiErrorMessage(err: unknown): string {
  if (axios.isAxiosError(err)) {
    const server = (err.response?.data as { error?: string } | undefined)?.error;
    if (server) return server;
    if (!err.response) return 'Cannot reach DayPilot — check your connection.';
  }
  return 'Something went wrong. Please try again.';
}
