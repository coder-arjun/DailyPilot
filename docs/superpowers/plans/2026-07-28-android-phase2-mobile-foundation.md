# DayPilot Android — Phase 2: Mobile Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expo app in `mobile/` that signs in against the live Phase-1 API and shows/edits Today's tasks, with the DayPilot dark theme.

**Architecture:** OPBT conventions — expo-router routes in `src/app` (`(auth)` group + `(app)` tabs), `src/lib` (config, api client, theme), `src/stores` (zustand). JWT access token in memory + SecureStore, refresh token in SecureStore, single-flight refresh in an axios interceptor. TanStack Query for server state (offline-first persister deferred to Phase 4 polish).

**Tech Stack:** Expo SDK 57 (RN 0.86, TS), expo-router, axios, zustand, @tanstack/react-query, expo-secure-store, @react-native-async-storage/async-storage.

## Global Constraints

- API base: `https://dailypilot.runasp.net/api/v1` via `app.json` → `expo.extra.apiUrl` (expo-constants), hardcoded fallback (OPBT pattern). Auth response shape: `{ accessToken, accessTokenExpiresAtUtc, refreshToken, user: { id, userName, displayName, email, xp } }`. Error bodies `{ error }`.
- Android package/appId: `com.daypilot.app`; app name **DayPilot**; slug `daypilot`; scheme `daypilot`.
- `mobile/**` must be excluded from the web csproj globs (`DefaultItemExcludes`), same as `tests/**`.
- Theme tokens (from `wwwroot/css/site.css` dark palette): bg `#0a0f1d`, surface `#131a2b`, surface2 `#0f1525`, border `#232c40`, text `#e6eaf2`, muted `#93a0b6`, primary `#818cf8`, brand1 `#6366F1`, brand2 `#9333EA`, accent `#F59E0B`, radius 16 / 11.
- No emulator on this machine: verification = `npx tsc --noEmit` and `npx expo export --platform android` (bundle compiles) after each task; device testing is the user's final step.
- SecureStore keys: `daypilot_refresh_token`, `daypilot_access_token`.

---

### Task 1: Scaffold + repo integration

**Files:** Create `mobile/` via `npx create-expo-app@latest mobile`; Modify `DailyPilot.csproj` (`DefaultItemExcludes` + `;mobile/**`); Create `mobile/AGENTS.md` ("# Expo HAS CHANGED — Read the exact versioned docs at https://docs.expo.dev/versions/v57.0.0/ before writing any code."); run create-expo-app's `reset-project` if the template ships demo screens, keeping a blank `src/app`.

- [ ] Scaffold; move routes to `src/app` (set `"experiments": {"typedRoutes": false}` default; expo-router finds `src/app` automatically when present).
- [ ] `app.json`: name DayPilot, slug daypilot, scheme daypilot, `android.package com.daypilot.app`, `extra.apiUrl https://dailypilot.runasp.net/api/v1`, dark `userInterfaceStyle`, `backgroundColor #0a0f1d`.
- [ ] csproj exclude; `dotnet build DailyPilot.csproj` still green; `npx tsc --noEmit` green in mobile/.
- [ ] Commit `feat(mobile): Expo scaffold (DayPilot, com.daypilot.app)`.

### Task 2: lib + stores (config, theme, api client, auth store)

**Files:** Create `mobile/src/lib/config.ts`, `mobile/src/lib/theme.ts`, `mobile/src/lib/api/client.ts`, `mobile/src/stores/authStore.ts`. Add deps: `axios zustand @tanstack/react-query expo-secure-store @react-native-async-storage/async-storage` (via `npx expo install` for the expo ones).

**Interfaces (produced):**
- `config.apiUrl: string`
- `theme` object: `{ colors: { bg, surface, surface2, border, text, muted, primary, brand1, brand2, accent, danger: '#f87171', success: '#22c55e' }, radius: { md: 16, sm: 11 }, spacing(n: number): number /* n*4 */ }`
- `api` axios instance: baseURL = config.apiUrl, `X-Client: mobile`; request interceptor injects `Authorization: Bearer <accessToken>` from authStore; response interceptor on 401 (once, `_retried` flag) calls `authStore.getState().refresh()` then replays; on refresh failure calls `logout()`.
- `useAuthStore` (zustand): `{ status: 'loading'|'signedOut'|'signedIn', user: {id,userName,displayName,email,xp}|null, accessToken: string|null, hydrate(): Promise<void>, login(login,password): Promise<void>, register(fields): Promise<void>, refresh(): Promise<void>, logout(): Promise<void> }`; `hydrate` reads SecureStore refresh token → POST /auth/refresh via a **bare** axios instance (no interceptors) → stores rotated tokens; failures → signedOut. Errors thrown carry `err.response?.data?.error ?? 'Something went wrong'`.

- [ ] Implement the four files with the exact shapes above; `tsc --noEmit` green; commit `feat(mobile): theme, api client, auth store`.

### Task 3: Routing shell + auth screens

**Files:** Create `mobile/src/app/_layout.tsx` (QueryClientProvider + splash-hold until `hydrate()` resolves + redirect: signedIn→`/(app)`, signedOut→`/(auth)/login`), `mobile/src/app/(auth)/_layout.tsx`, `(auth)/login.tsx`, `(auth)/register.tsx`, `mobile/src/app/(app)/_layout.tsx` (Tabs: Today; more tabs land in Phase 4), shared UI `mobile/src/components/ui.tsx` (`Screen`, `Card`, `Button`, `Input`, `Label`, `ErrorText` — dark theme styled).

- [ ] Login: username/email + password, brand header ("DayPilot — Plan. Complete. Move Forward."), error text from thrown message, link to register. Register: displayName, userName, email, password (+confirm client-side), timeZoneId auto from `Intl.DateTimeFormat().resolvedOptions().timeZone` mapped: send the IANA id — **server expects Windows ids**; add `timeZoneId` mapping note: POST the IANA string; server `TimeZoneInfo.TryFindSystemTimeZoneById` accepts IANA ids on .NET 8+ (ICU) — verified during execution with a live register call.
- [ ] `npx expo export --platform android` succeeds; commit `feat(mobile): auth flow + tabs shell`.

### Task 4: Today screen + task editor

**Files:** Create `mobile/src/features/tasks/api.ts` (typed hooks: `useTodayTasks(date?)`, `useCreateTask`, `useUpdateTask`, `useDeleteTask`, `useCompleteTask`, `useReopenTask`, `useCategories` — react-query over the Phase-1 endpoints, DTO types mirroring TaskDto/CategoryDto), `mobile/src/features/tasks/TaskList.tsx`, `TaskRow.tsx` (checkbox toggle → complete/reopen, priority pill, category dot, due time), `mobile/src/app/(app)/index.tsx` (Today: header with date, list, FAB → editor modal), `mobile/src/app/(app)/task-editor.tsx` (modal route: create/edit form — title, notes, date [@react-native-community/datetimepicker], due/reminder time, priority selector, category picker; delete button on edit).

- [ ] Implement; pull-to-refresh via query invalidation; optimistic complete-toggle (onMutate cache update, rollback on error).
- [ ] `tsc --noEmit` + `expo export` green; live check: script-register a throwaway user, then GET tasks with its token to confirm the same API the app calls (server already smoke-tested; this validates the client's URL/shape assumptions via one `node -e` fetch using the app's config module if runnable, else curl with identical paths).
- [ ] Commit `feat(mobile): Today screen + task editor`.

### Task 5: Phase checkpoint

- [ ] `dotnet build` + `dotnet test` still green (server untouched but guard the csproj exclude).
- [ ] Update this plan's checkboxes + memory note; commit `chore: phase 2 checkpoint`.

**Self-review:** covers spec Phase-2 scope (scaffold, theme, auth screens+store, API client, Today+editor); no placeholders; interface names consistent (authStore shape, theme keys, hook names used by screens).
