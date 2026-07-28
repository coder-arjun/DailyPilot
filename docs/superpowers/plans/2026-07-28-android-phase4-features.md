# DayPilot Android — Phase 4: Feature Build-out Plan (compact)

> Executed via parallel subagents, one per feature batch; orchestrator wires shared
> files ((app)/_layout tabs, Program.cs if ever needed) and commits after review.
> Every batch follows the established patterns — server: `Controllers/Api/TasksApiController.cs`
> + `Controllers/Api/Dtos/`; mobile: `src/features/tasks/api.ts`, `src/app/(app)/index.tsx`.

**Global rules (every batch):** API controllers extend `ApiControllerBase` (JWT scheme
"Api", `[IgnoreAntiforgeryToken]`, `CurrentUserId`); errors `{error}` with 400/404/409;
DTOs are records with `From(entity)` mappers; call EXISTING services — read the service
interface before writing the controller; no service-behavior changes. Mobile: theme
tokens only, react-query hooks in `src/features/<name>/api.ts`, screens under
`src/app/(app)/`, `npx tsc --noEmit` must pass. No commits by subagents; terse report.

## Batches

1. **Habits** — `GET/POST api/v1/habits`, `POST habits/{id}/checkin` (+uncheck),
   streak fields from `IHabitService`; mobile Habits tab (list, check-in ring,
   streak flame, add-habit sheet).
2. **Dashboard + Search + History** — `GET api/v1/dashboard` (via `IAnalyticsService`
   + `IGamificationService`: completion stats, streak, XP/level), `GET api/v1/search?q=`,
   `GET api/v1/history?page=`; mobile Dashboard tab (stat cards + weekly bar chart in
   react-native-svg), Search screen, History screen (route from Settings).
3. **Calendar + Board** — `GET api/v1/tasks/calendar?month=yyyy-MM` (day→counts+tasks),
   `GET api/v1/tasks/board` + `POST tasks/{id}/status`; mobile Calendar tab (month grid
   + day list) and Board screen (status columns, tap to move).
4. **Settings + Route Planner + AI** — `GET/PUT api/v1/account/settings` (reminder
   times/toggles from ApplicationUser), `POST api/v1/routeplanner/resolve` (JWT twin of
   the MVC Resolve — reuse `MapsLinkParser` + "maps-resolver" client),
   `POST api/v1/ai/chat` (over `IAiTaskAssistant`); mobile Settings tab (profile, logout,
   "on the web" list), native Route Planner screen (geolocate + stops + haversine ranking
   + Google Maps intent via `Linking.openURL`), AI chat screen.

Deferred from v1 (per spec): attachments, invitations, workspace admin, backups,
workspace switching (needs `X-Workspace-Id` plumbing — Phase 5 stretch).

**Checkpoint per batch:** server `dotnet build` + `dotnet test` green; mobile `tsc`
green; orchestrator wires tabs, runs `expo export --platform android`, commits.
