# DayPilot Android App (React Native) — Design Spec

**Date:** 2026-07-28
**Status:** Approved (full native rewrite, OPBT-style architecture)
**Deliverable:** signed release APK at `build\DayPilot.apk` + deployed server API

## Goal

A native Android app with feature parity with the DailyPilot web app, whose headline
improvement is **reliable background push notifications** (FCM), which the current
PWA cannot deliver dependably on Android. Architecture mirrors `D:\MyProject\OPBT\mobile`
(Expo SDK 57, expo-router, TypeScript, TanStack Query offline-first, zustand,
SecureStore JWT auth) — but with the push pipeline actually completed, which OPBT
left unwired (no google-services.json, no token registration, no FCM sender).

## Architecture

Two workstreams in this repo:

1. **Server: REST API layer** (`/api/v1/...`) in the existing ASP.NET Core app —
   thin controllers over the existing services (TaskService, HabitService, etc.).
   No behavior duplication: API controllers call the same services the MVC
   controllers use. JWT bearer auth for the API only; cookie/Identity auth for the
   web stays untouched.
2. **Mobile: Expo app** at `mobile/` (excluded from the web csproj globs like
   `tests/`). OPBT folder conventions: `src/app` (expo-router routes),
   `src/features`, `src/lib` (api client, theme, config), `src/stores` (zustand).

### Auth (OPBT pattern, adapted)

- `POST /api/v1/auth/login` (username/email + password via Identity's
  SignInManager check), `register`, `refresh`, `logout`.
- Access token: JWT, 15 min, signed with a server secret (`Jwt:Secret` in server
  appsettings — generated during deploy, never committed).
- Refresh token: opaque, rotated on use, stored hashed in a new `dbo.ApiRefreshTokens`
  table; mobile stores it in expo-secure-store; `hydrate()` on boot calls refresh.
- Mobile sends `X-Client: mobile`; axios interceptor refreshes once on 401.

### Push pipeline (the part OPBT never built)

- New `dbo.DeviceTokens` table: UserId, FcmToken (unique), Platform, CreatedAt,
  LastSeenAt. Migration is additive, `dbo` schema only (shared-DB contract intact).
- `POST /api/v1/devices` registers/refreshes a token (JWT auth);
  `DELETE /api/v1/devices` on logout.
- Server: `FcmPushSender` using the **FirebaseAdmin** NuGet package with a
  service-account JSON placed at `/private/firebase-service-account.json` on the
  server (path via `Fcm:CredentialPath` config; feature disabled gracefully when
  missing). `ReminderDispatchService` sends to WebPush subscriptions AND FCM
  device tokens; invalid/unregistered FCM tokens are pruned on send failure.
- Mobile: `@react-native-firebase/app` + `@react-native-firebase/messaging`
  (config-plugin, google-services.json from the user's Firebase project;
  package name `com.daypilot.app`), Android 13 POST_NOTIFICATIONS runtime prompt,
  a "reminders" notification channel (high importance), `onNotificationOpenedApp` /
  `getInitialNotification` deep-links to the relevant screen via a `route` data field.
  Data-only? No: send `notification` + `data` payloads so delivery works with the
  app killed without headless JS.

### Feature parity map (native screens)

| Screen (expo-router) | API endpoints (all `/api/v1`) |
|---|---|
| (auth) login / register | auth/* |
| Today (tabs/index) — list, complete, snooze/carry-forward | tasks?date=, tasks/{id}/complete, tasks (POST/PUT/DELETE) |
| Task editor (modal) — title, notes, date/time, priority, category, reminder | tasks, categories |
| Board (kanban) | tasks/board, tasks/{id}/status |
| Matrix (eisenhower) | tasks/matrix |
| Habits — list, check-in, streaks | habits, habits/{id}/checkin |
| Calendar — month grid + day list | tasks/calendar?month= |
| Dashboard — stats, streak, XP/level (react-native-svg charts, OPBT charts.tsx pattern) | dashboard |
| Assistant (AI chat) | ai/chat |
| Search | search?q= |
| History | history |
| Workspaces — list/switch (workspace context via header `X-Workspace-Id`) | workspaces |
| Route Planner — native form, reuses server Resolve; "Open in Google Maps" intent | routeplanner/resolve |
| Settings — profile, reminder times, notification toggles, logout | account, account/settings |
| Pomodoro — fully client-side timer with local notification on finish | — |

Deferred (not in v1 APK; web app remains available for them): attachments
upload, invitations management, workspace admin (create/invite), category
color management UI beyond selection, backups. These are listed on the Settings
screen as "available on the web".

### Mobile foundations (copied from OPBT)

- Expo SDK 57 / RN 0.86 / TS; `newArchEnabled`, Hermes.
- TanStack Query + AsyncStorage persister, `networkMode: 'offlineFirst'`,
  NetInfo→onlineManager, AppState→focusManager.
- zustand stores: auth, workspace context.
- `app.json` `extra.apiUrl` = `https://dailypilot.runasp.net/api/v1` (expo-constants).
- Theme: DayPilot Slate & Titanium (dark) — tokens ported from `site.css`
  (`#0a0f1d` bg, `#131a2b` surface, indigo→violet brand, amber accent, Inter via
  expo-font).
- `mobile/AGENTS.md` pointing at the versioned Expo 57 docs (OPBT's guard).

### Build & signing (fixing OPBT's shortcuts)

- Proper **release keystore** generated at `mobile/android-signing/daypilot-release.keystore`
  (gitignored; passwords in `mobile/android-signing/credentials.txt`, gitignored —
  user must back these up; losing them means losing update continuity).
- `versionCode`/`versionName` maintained in `app.json`.
- Build: `npx expo prebuild --platform android` + `gradlew assembleRelease`
  (SDK at `C:/Android/sdk`); ABI split limited to `arm64-v8a,armeabi-v7a` to
  halve OPBT's 114 MB APK. Output copied to `build\DayPilot.apk`.

## Phases (each ends at a working checkpoint)

1. **API foundation** — JWT auth (+refresh table migration), tasks + categories
   endpoints, xunit tests for auth + tasks. Server deployable.
2. **Mobile foundation** — Expo scaffold, theme, auth screens + store, API client,
   Today + task editor working against the live API.
3. **Push end-to-end** — DeviceTokens migration, FirebaseAdmin sender, dispatch
   integration, mobile messaging + channel + deep links. (Needs the user's two
   Firebase files; code lands regardless, activates when files appear.)
4. **Feature build-out** — Habits, Calendar, Dashboard, Board, Matrix, Search,
   History, Workspaces switch, AI chat, Route Planner, Settings, Pomodoro.
5. **Polish & ship** — icon/splash (DayPilot brand), offline states, release
   keystore, APK at `build\DayPilot.apk`, server deploy (new NuGet DLLs +
   deps.json per FTP memory), smoke verification.

## Error handling & testing

- API: consistent `{ error: string }` bodies, ProblemDetails avoided for
  simplicity; 401 drives the refresh flow; server tests cover auth round-trip,
  refresh rotation, task CRUD authorization (user isolation), device-token
  upsert, and FCM sender token-pruning logic (FirebaseAdmin mocked).
- Mobile: TS strict; runtime testing via Today-screen flows against prod API;
  final manual test by the user (login, complete task, receive reminder with
  app killed, tap → correct screen).

## Non-negotiable constraints

- Shared prod DB: migrations additive, `dbo` only, never touch `finoma` schema.
- Web app behavior unchanged (cookie auth, all pages, WebPush keep working).
- No secrets in git: keystore, Jwt:Secret, firebase-service-account.json,
  google-services.json all gitignored / server-side only.
