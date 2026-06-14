# ADR 0005 — Multi-user (family) support

**Date:** 2026-06-04  
**Status:** Accepted  
**Note:** The "active member via `bp_member` cookie (deferred login)" stepping-stone
decided here was superseded by [ADR-0006](0006-per-member-authentication.md)
(per-member authentication). The data-model and SQLite decisions remain valid.

## Context

BpMonitor was originally a single-stream app: one global `Readings` table with no notion of ownership. The product needs to serve a family — each member tracking their own blood pressure independently.

Two design questions arose:

1. **Database engine:** Should we switch from SQLite to Postgres to support multiple users?
2. **Active-member mechanism:** Without a login system (deferred), how should the app know which family member is "active"?

## Decisions

### 1. Stay on SQLite — multi-user is a data-model change, not a DB-engine change

"Multi-user" for a family means a handful of profiles and very light, mostly-serial write load. SQLite's only real constraint is concurrent *writers* under heavy load. With WAL mode enabled and per-request scoped `DbContext`, even a busy family's write pattern is well within SQLite's capabilities.

Postgres would add an external server, credentials, container, backup strategy, and connection management for zero practical benefit at this scale. The correct response to "do we need Postgres?" is: "only when we genuinely have many concurrent writers or require DB-level features SQLite can't provide."

**Consequence:** multi-user support is implemented as a data-model change:
- New `Members` SQLite table and `FamilyMember` domain type
- `MemberId` foreign key on `Readings`
- All repository read/write methods are now member-scoped

### 2. Active member via `bp_member` cookie — explicit stepping stone for future auth

Login is explicitly deferred. The active member is determined by a `bp_member` cookie (HttpOnly, SameSite=Strict) set when the user picks a profile from `/members`. If no cookie is set (first visit, new browser), the app falls back to the first family member.

This is a deliberate stepping stone: when real authentication is added later, only the active-member resolver needs to change (read the session/JWT claim instead of the cookie). The data model, repository interfaces, and handler logic remain unchanged.

### 3. Existing data backfill

Schema migration (`SchemaMigrations.apply`) seeds a default member ("Me") on first run and back-fills all existing readings to that member's ID, so current data stays visible without manual intervention.

### 4. WAL mode enabled

WAL + `busy_timeout=5000` are now applied at startup (they were previously assumed but not actually set). This was the right moment to close that gap.

## Alternatives considered

- **Postgres**: ruled out — see above
- **Route-segment per member** (`/members/{id}/history`): explicit and bookmarkable, but rewrites every route, nav link, and the chart iframe URL. Cookie-based selection keeps URLs stable and is simpler to layer auth on top of.
- **Query param `?member=`**: lightweight but ugly URLs, easy to lose the selection, fragile with htmx boosting.

## Consequences

- `IReadingRepository` is now member-scoped: `GetAll memberId`, `Add memberId`, `AddMany memberId`, `Update` (guards by `reading.MemberId`)
- `BloodPressureReading` has a new `MemberId: int` field (defaults to 0 from `parse`; stamped with the real ID at persist time)
- `InMemoryFamilyMemberRepository` is used in tests; `EfFamilyMemberRepository` in production
- Import/Export functions gained a `memberId` parameter
- Login, per-member permissions, member delete/merge, and per-member chart colours are out of scope for this iteration
