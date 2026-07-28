# Expo HAS CHANGED

Read the exact versioned docs at https://docs.expo.dev/versions/v57.0.0/ before writing any code.

## DayPilot mobile (Expo SDK 57, RN 0.86, TypeScript, expo-router)

- API: `https://dailypilot.runasp.net/api/v1` (JWT bearer; see `src/lib/api/client.ts`). Auth/refresh contract documented in `../docs/superpowers/plans/2026-07-28-android-phase2-mobile-foundation.md`.
- Layout: `src/app` routes, `src/features` domain UI, `src/lib` (config/theme/api), `src/stores` (zustand).
- Theme tokens in `src/lib/theme.ts` — mirror the web app's Slate & Titanium palette; do not invent new colors.
- Android package `com.daypilot.app`. Push (FCM) lands in Phase 3 and requires `google-services.json` in this directory.
