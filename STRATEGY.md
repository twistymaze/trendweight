---
name: TrendWeight
last_updated: 2026-10-04
---

# TrendWeight Strategy

## Purpose

People who weigh themselves daily see a number that swings with water weight from one
day to the next. That noise hides whether their weight is really trending up, down, or
holding steady, so it is hard to know where they actually stand.

## Positioning

Do one thing: show the real trend, using transparent, explainable smoothing math (the
Hacker's Diet method by default) rather than a vendor's black box, fed straight from the
scale. Everything else the scale apps and all-in-one trackers do is left out on purpose.
TrendWeight was the first web app to do this (15+ years ago), and its long-time users
have no reason to switch.

## Users

**Primary:** Daily weighers whose goals shift over the years - losing in one season,
maintaining in another. The author is one of them, but the ~1,300 monthly active users
(~900 retained across months) are who the product serves. They're hiring TrendWeight
to show where their weight is really heading against their goal, with no daily effort
beyond stepping on the scale or typing in a number.

## Boundaries

- No coach or multi-client views; sharing stays per user.
- No trends for measurements beyond what the scale reports (no waist, blood pressure, or steps).
- No food, calorie, or exercise logging.
- No separate native trend UI; a companion app exists to get readings in, and hosts the existing website for everything else.

_Resist a change when:_ it serves someone other than an individual tracking their own
weight, or tracks something the scale doesn't measure.

## Key metrics

- **Monthly active users** - users active in the month; Clerk dashboard.
- **Retained users** - users active across multiple months; Clerk dashboard.
- **Recent-reading share** - share of active users with a new reading (any source) in the last 7 days; catches silent data-flow loss before MAU moves; Supabase.
- **User feedback** - complaints, requests, and reactions to changes; email and GitHub issues.

## Tracks

### Getting readings in

Withings, the weight log, and the API, so no user loses their data flow (the Fitbit replacement work lived here).

_Why it serves the approach:_ The trend is only as good as the daily readings feeding it.
Try the lowest-maintenance path first (API plus documented recipes); a small companion
app is acceptable when it is the only way to a good experience.

### Low-touch operations

Automated dependency updates, cost, and reliability, so one person can keep it running for free.

_Why it serves the approach:_ Doing one thing only works if that one thing stays up
without becoming a second job.

### Trend clarity

Charts, stats, and the math, plus context that helps explain a reading.

_Why it serves the approach:_ The trend is the product. Aids that explain it are in
scope but never replace the math; anything that adds per-user cost or sends health
data to a third party (e.g. AI insights) stays opt-in.
