# Sleep Tracker — Design Spec

**Date:** 2026-07-30
**Source:** user requirements in `Sleep tracker.txt` (fields, score weights, dashboard mock, insight examples)
**Targets:** web (MVC), REST API, Android app — feature parity.

## What it is (and honestly isn't)

A manual sleep journal: the user records evening (bed time, estimated sleep time,
phone-use-before-bed flag) and morning (wake time, time out of bed, quality 1–10,
dream-remembered flag) values; everything else is computed. No sensor-based sleep
detection is claimed anywhere. "AI Insights" = a deterministic pattern-analysis
engine producing personalized observations from the user's own data; LLM-polished
phrasing activates only if `Ai:Provider` is configured (production today: None).

## Data

`dbo.SleepEntries` (additive migration; shared-DB contract: dbo only):
Id, UserId (FK cascade), **Date** (the wake-up date; unique per user+date),
BedTime (TimeOnly), EstimatedSleepTime (TimeOnly), WakeTime (TimeOnly),
TimeOutOfBed (TimeOnly), Quality (1–10), PhoneBeforeBed (bool?), DreamRemembered
(bool?), Notes (≤500, optional), CreatedAtUtc.

Client conveniences: EstimatedSleepTime defaults to BedTime+15m; TimeOutOfBed
defaults to WakeTime. Evening and morning can be saved separately (upsert by date;
morning fields nullable until filled — an entry is *complete* when WakeTime and
Quality exist; metrics use complete entries only).

## Formulas (pinned; times are user-local; all "mod 24 h" for midnight wrap)

- TimeInBed = (TimeOutOfBed − BedTime) mod 24h; SleepDuration = (WakeTime −
  EstimatedSleepTime) mod 24h; Efficiency = min(100, SleepDuration/TimeInBed×100).
- Trailing window = last 7 complete entries (≥2 required for averages, ≥3 for
  consistency/insights; below that the UI shows "log more nights").
- Average bed/wake time = **circular mean** (handles 23:30 vs 00:30 correctly).
- Sleep Debt = Σ max(0, 480min − duration) over the window (target fixed 8h in v1).
- Consistency = max(0, 100 − avg(circStdDev(bed), circStdDev(wake))/90×100)
  (stddev in minutes; 0min→100%, ≥90min→0%). Wake Regularity = same on wake only.
- Weekend Shift = |circMean(bed on Fri+Sat nights) − circMean(bed on other nights)|.
- Late Sleep Days = bedtime in [00:00,12:00) count; Early Wake Days = wake < 06:00.
- **Sleep Score** (user's weights): 0.30×durationPts + 0.25×consistency +
  0.20×efficiency + 0.15×wakeRegularity + 0.10×(quality×10), where durationPts =
  max(0, 100 − |duration−480|/180×100). Bands: ≥85 Excellent, 70 Good, 55 Fair,
  else Poor. Stars = round(score/20).
- Ideal Bed Time = circMean(wake) − 8h − 15m latency.

## Insights engine (deterministic rules; each fires only past its data threshold)

Weekly observations: week-over-week Δ average sleep ("18 minutes longer"),
Δ consistency, best-quality day, shortest-sleep day. Personalized patterns
(≥14 entries): weekday-past-midnight ratio ≥50% → bedtime-shift suggestion with
computed weekly gain; quality≥8 duration band ("best mornings after 7h45–8h15");
Sunday bedtime ≥45m later than weekday mean → Monday-effect note; phone-before-bed
false nights ≥1 quality point better (≥3 samples each) → screen note. Rules emit
the exact sentence styles from the requirements file. When `Ai:Provider` is
configured, the emitted facts are re-phrased by the assistant; otherwise the
template phrasing ships (current production state).

## Surfaces

- **Server core:** `Services/Sleep/SleepMetrics.cs` (pure static math — fully unit
  tested), `Services/Sleep/SleepInsights.cs` (pure rules — unit tested),
  `Services/Sleep/SleepService.cs` (EF orm: upsert/query/window assembly).
- **API** (`api/v1/sleep`): PUT `entry` (upsert by date), GET `entry?date=`,
  GET `dashboard` (today card + averages + debt + consistency + ideal bedtime +
  score), GET `insights`, GET `entries?from=&to=` (history/trend), DELETE `entry?date=`.
- **Web:** `SleepController` + views — Sleep page matching the mock (score card
  with stars, Average This Week / Sleep Debt / Consistency / Ideal Bed Time cards,
  evening/morning entry forms, weekly insights list, 14-day duration bar strip).
  Nav: More → Sleep (`bi-moon-stars`), command palette entry.
- **Mobile:** new Sleep tab (`moon-outline`) — dashboard cards per the mock
  ("Simple. Minimal. Beautiful."), evening/morning entry sheets with time pickers,
  insights section, 14-day mini bar strip. Follows theme tokens + ui kit.

## Testing

TDD for SleepMetrics + SleepInsights (circular stats, midnight wraps, score
weights, band edges, each insight rule's guard). API verified via extended
`scripts/api-smoke.ps1` sleep section against prod post-deploy. Mobile: tsc +
expo export + user device pass.

## Out of scope (v1)

Configurable sleep target (fixed 8h), wearable/sensor import, reminders to log
(the existing reminder system is unchanged), sleep-phase claims of any kind.
