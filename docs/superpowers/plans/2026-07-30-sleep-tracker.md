# Sleep Tracker Implementation Plan (compact)

> Spec: `docs/superpowers/specs/2026-07-30-sleep-tracker-design.md` — formulas and
> thresholds are pinned THERE; every task consumes them verbatim. Established
> conventions apply (ApiControllerBase, `{error}`, theme tokens, ui kit, tsc/export
> gates, additive dbo-only migrations).

1. **Math core (TDD, orchestrator-inline):** `Services/Sleep/SleepMetrics.cs` +
   `Services/Sleep/SleepInsights.cs`, pure static; tests for circular mean/stddev
   (23:30/00:30 wrap), durations across midnight, efficiency cap, score weights +
   band edges, debt, weekend shift, late/early counts, every insight rule guard.
2. **Persistence + API (inline):** `Models/SleepEntry` + DbSet + unique(UserId,Date)
   index + `AddSleepEntries` migration (inspect: dbo only); `SleepService` (upsert
   by date, window assembly); `SleepApiController` (PUT/GET entry, dashboard,
   insights, entries range, DELETE) per spec.
3. **Web UI (subagent):** `SleepController` + `Views/Sleep/Index.cshtml` matching
   the mock (score card + stat cards + entry forms + insights + 14-day strip),
   `wwwroot/js/sleep.js` as needed; no edits to shared files (orchestrator wires
   nav + palette).
4. **Mobile (subagent):** `src/features/sleep/api.ts` + `src/app/(app)/sleep.tsx`
   (dashboard cards, evening/morning sheets with time pickers, insights, bar strip);
   no shared-file edits (orchestrator wires the tab).
5. **Wire + ship (inline):** nav/More + palette + mobile tab; extend
   `scripts/api-smoke.ps1` (sleep upsert→dashboard→insights); dotnet build/test,
   tsc/export; deploy DLL (migration auto-runs); prod smoke; rebuild APK →
   `build\DayPilot.apk`.
