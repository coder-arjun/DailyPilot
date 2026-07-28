import axios from 'axios';
import * as SecureStore from 'expo-secure-store';
import { create } from 'zustand';
import { config } from '@/lib/config';

const REFRESH_KEY = 'daypilot_refresh_token';
const ACCESS_KEY = 'daypilot_access_token';

export type AuthUser = {
  id: string;
  userName: string;
  displayName: string;
  email: string;
  xp: number;
};

type TokenResponse = {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  user: AuthUser;
};

export type RegisterFields = {
  displayName: string;
  userName: string;
  email: string;
  timeZoneId: string;
  password: string;
};

type AuthState = {
  status: 'loading' | 'signedOut' | 'signedIn';
  user: AuthUser | null;
  accessToken: string | null;
  hydrate: () => Promise<void>;
  login: (login: string, password: string) => Promise<void>;
  register: (fields: RegisterFields) => Promise<void>;
  refresh: () => Promise<void>;
  logout: () => Promise<void>;
};

/** Interceptor-free client — used for auth calls so a 401 here can never recurse. */
const bare = axios.create({
  baseURL: config.apiUrl,
  headers: { 'Content-Type': 'application/json', 'X-Client': 'mobile' },
  timeout: 20000,
});

function messageOf(err: unknown): string {
  if (axios.isAxiosError(err)) {
    const server = (err.response?.data as { error?: string } | undefined)?.error;
    if (server) return server;
    if (!err.response) return 'Cannot reach DayPilot — check your connection.';
  }
  return 'Something went wrong. Please try again.';
}

/** Deduplicates concurrent refresh attempts (single-flight). */
let refreshing: Promise<void> | null = null;

export const useAuthStore = create<AuthState>((set, get) => {
  async function applyTokens(data: TokenResponse) {
    await SecureStore.setItemAsync(REFRESH_KEY, data.refreshToken);
    await SecureStore.setItemAsync(ACCESS_KEY, data.accessToken);
    set({ status: 'signedIn', user: data.user, accessToken: data.accessToken });
  }

  async function clearTokens() {
    await SecureStore.deleteItemAsync(REFRESH_KEY);
    await SecureStore.deleteItemAsync(ACCESS_KEY);
    set({ status: 'signedOut', user: null, accessToken: null });
  }

  return {
    status: 'loading',
    user: null,
    accessToken: null,

    hydrate: async () => {
      try {
        const stored = await SecureStore.getItemAsync(REFRESH_KEY);
        if (!stored) {
          set({ status: 'signedOut' });
          return;
        }
        const { data } = await bare.post<TokenResponse>('/auth/refresh', { refreshToken: stored });
        await applyTokens(data);
      } catch {
        await clearTokens();
      }
    },

    login: async (login, password) => {
      try {
        const { data } = await bare.post<TokenResponse>('/auth/login', { login, password });
        await applyTokens(data);
      } catch (err) {
        throw new Error(messageOf(err));
      }
    },

    register: async (fields) => {
      try {
        const { data } = await bare.post<TokenResponse>('/auth/register', fields);
        await applyTokens(data);
      } catch (err) {
        throw new Error(messageOf(err));
      }
    },

    refresh: async () => {
      refreshing ??= (async () => {
        try {
          const stored = await SecureStore.getItemAsync(REFRESH_KEY);
          if (!stored) throw new Error('no refresh token');
          const { data } = await bare.post<TokenResponse>('/auth/refresh', { refreshToken: stored });
          await applyTokens(data);
        } catch (err) {
          await clearTokens();
          throw err;
        } finally {
          refreshing = null;
        }
      })();
      await refreshing;
    },

    logout: async () => {
      const stored = await SecureStore.getItemAsync(REFRESH_KEY);
      if (stored) {
        try {
          await bare.post('/auth/logout', { refreshToken: stored });
        } catch {
          // best effort — local sign-out proceeds regardless
        }
      }
      await clearTokens();
    },
  };
});
