# Email feedback intake into GitHub issues

Captured 2026-10-04 from a conversation about `ce-sweep`. Not started; seed for a
future `ce-brainstorm`.

## Problem

Most TrendWeight feedback arrives by email, with some on GitHub issues. `ce-sweep`
handles GitHub issues well (it labels items `feedback:ack` / `feedback:resolved`
and checks claimed fixes against `main`). Its email source is experimental and
read-only: it cannot label or move messages, so tracking lives only in the sweep's
state file and nothing in Gmail shows what was handled.

## Idea

Treat email as an intake channel and GitHub Issues as the single place work is
tracked. An intake step turns actionable emails into issues; `ce-sweep` then
watches GitHub only.

1. **Read** new emails under the TrendWeight Gmail label.
2. **Classify** each as actionable (bug, feature request, data problem) or not
   (thanks, already-answered question, chit-chat). Interactive runs show the split
   for confirmation; unattended runs create issues only for clear bugs and hold
   the rest.
3. **Mark processed in Gmail** with labels, e.g. `trendweight/triaged` on every
   email reviewed and `trendweight/issued` on those that became issues. Query:
   "TrendWeight label and not triaged". Labels beat a timestamp alone: replies in
   old threads and late-arriving mail are still caught, and the state is visible
   and fixable by hand in Gmail.
4. **Follow-ups**: store the Gmail thread ID in a hidden comment in the issue body,
   so a later reply in the same thread adds a comment instead of a new issue.

## Privacy

The repo (`twistymaze/trendweight`) is public. Issues restate the request or
feedback generically, on behalf of the sender, with nothing that identifies them:
no name, email address, weights, or health details. The issue links back to the
email only by thread ID.

## Where it would live

A project skill in `.agents/skills/` (e.g. `feedback-intake`) that runs before
`/compound-engineering:ce-sweep`. Not an edit to `ce-sweep` itself: it lives in
the plugin cache and is overwritten on update. Rejected alternatives:

- Forking `ce-sweep` into the repo: one command, but a large skill to maintain
  without upstream fixes.
- Proposing it upstream to the Compound Engineering plugin: right long-term home
  if useful to others, but slow and outside our control. Revisit once the wrapper
  has proven itself.

## Open questions

- Exact classification rules, and what "clear bug" means for unattended runs.
- Closing the loop: closing an issue tells the emailer nothing, and the sweep never
  sends email. Option: when an issue resolves, draft (not send) a Gmail reply for
  review.
- Whether `ce-sweep` should still watch email at all once intake exists.
