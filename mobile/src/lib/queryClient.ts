import { QueryClient } from '@tanstack/react-query';

/** Module-level singleton so it can be imported (and cleared) outside of React,
 * e.g. from authStore's clearTokens() on logout/auth-failure. */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1 },
  },
});
