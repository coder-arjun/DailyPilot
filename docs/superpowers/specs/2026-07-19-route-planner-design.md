# Route Planner — Design Spec

**Date:** 2026-07-19
**Status:** Approved (free/no-API-key approach, 2–5 stops, standalone nav page)

## Purpose

A standalone "Route Planner" page where the user shares their current location (point A),
pastes 2–5 Google Maps locations, and gets:

1. Every destination ranked by distance from A, nearest first, with the nearest highlighted.
2. One button that opens the Google Maps app/site with the combined route
   A → nearest → … → farthest, giving real turn-by-turn navigation.

No Google API key, no billing, no database changes, no new NuGet packages.

## Decisions made during brainstorming

- **Free approach** over Google Maps JS API or Leaflet/OSRM: distances are computed
  in-app with the haversine formula (straight-line); the combined route is handed to
  the real Google Maps app via a standard directions deep link. Zero cost.
- **2–5 stops** (not fixed two): two destination fields plus "Add stop" up to five.
- **Standalone page** in the nav (More dropdown), not attached to Tasks.
- **Premium UI**: match DayPilot's existing design system (`--dp-*` tokens, brand
  gradient, `--dp-ease` easing) with smooth animations and modern components.

## Architecture

### Server

- `Services/MapsLinkParser.cs` — static, pure. Extracts `(lat, lng)` from:
  - full URLs containing `@lat,lng,zoom`
  - place URLs containing `!3d<lat>!4d<lng>` (preferred over `@` when both present —
    `!3d/!4d` is the pin, `@` is the viewport)
  - `?q=lat,lng` / `?query=lat,lng` search URLs
  - raw `lat,lng` text
  Also provides `HaversineKm(lat1, lng1, lat2, lng2)`. Validates lat ∈ [−90, 90],
  lng ∈ [−180, 180]. Culture-invariant parsing.
- `Controllers/RoutePlannerController.cs` — `[Authorize]`.
  - `GET Index` → the page.
  - `POST Resolve` (AJAX, antiforgery): body = pasted text. If it parses directly,
    return coordinates. If it is a short link (`maps.app.goo.gl`, `goo.gl/maps`),
    follow redirects server-side with `IHttpClientFactory` (browsers cannot read
    cross-origin redirect targets), then parse the final URL. Returns JSON
    `{ ok, lat, lng, label, error }`. Label is the place name segment of the URL
    (`/maps/place/<name>/`) when present, URL-decoded.
  - Timeout ~5 s on the outbound request; failures return `ok=false` with a clear message.
- `Program.cs` — add `builder.Services.AddHttpClient()` (IHttpClientFactory).

### Client

- `Views/RoutePlanner/Index.cshtml` + `wwwroot/js/route-planner.js` + styles in `site.css`.
- "Use my location" → browser Geolocation API (HTTPS + PWA already in place).
  Manual fallback: point A accepts a pasted link/coords like any stop.
- Stops: 2 fields initially, "Add stop" to 5, removable. Each field resolves on
  paste/blur via `Resolve`, showing a resolved chip (label + coords) or inline error.
- "Compare" → haversine in JS, animated ranked list (nearest highlighted, distances
  in km; staggered reveal animation).
- "Open route in Google Maps" → `https://www.google.com/maps/dir/?api=1&origin=A
  &waypoints=stop1|stop2…&destination=last` with stops ordered nearest-first.
  On mobile this opens the Google Maps app with the multi-stop route.
- Small note in UI: distances are straight-line ("as the crow flies").

### Nav

- Link in `_Layout.cshtml` More dropdown (`bi-signpost-split` icon), participates in
  `Active("RoutePlanner")` highlighting.

## Error handling

- Unparseable input → inline field error: "Couldn't read coordinates — paste the full
  Google Maps URL or lat,lng."
- Geolocation denied/unavailable → show manual entry for A with a hint.
- Short-link expansion failure (network/timeout) → `ok=false` message shown inline.
- Compare/route buttons disabled until A and ≥2 stops resolve.

## Testing

- xunit project `tests/DailyPilot.Tests` referencing the app project.
  Unit tests for `MapsLinkParser`: each URL shape, `!3d!4d` preferred over `@`,
  invalid/out-of-range input rejected, negative coordinates, haversine sanity
  (known city pair ± tolerance).
- Manual: geolocation prompt, short-link paste, deep link on phone.

## Known limitation (stated in UI)

"Nearest" is straight-line, not road distance. Route order is nearest-first by that
measure; Google Maps shows true road routing once opened.

## Out of scope

Persistence of routes, place-name search/geocoding, more than 5 stops, embedded map
tiles, travel-mode selection (deep link defaults to driving; Google Maps lets the
user switch after opening).
