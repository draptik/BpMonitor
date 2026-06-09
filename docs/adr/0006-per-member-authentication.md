# ADR 0006 — Per-member authentication

**Date:** 2026-06-09  
**Status:** Accepted  
**Supersedes:** login-deferred note in ADR 0005

## Context

ADR 0005 introduced multi-user (family) support and explicitly deferred login, using an
unsigned plaintext `bp_member` cookie as a temporary stepping stone. Any user could freely
switch to any member's profile via `POST /members/switch`, which provided no privacy between
family members.

## Decision

Add **per-member password authentication** using:

- **Mechanism:** ASP.NET Core cookie authentication (`AddAuthentication().AddCookie()`), already
  in the `Microsoft.NET.Sdk.Web` shared framework — no new package.
- **Hashing:** PBKDF2-SHA256 via `System.Security.Cryptography.Rfc2898DeriveBytes` (BCL) with
  310 000 iterations and a random 32-byte salt. Encoded as `iterations.base64salt.base64hash`.
  Constant-time comparison via `CryptographicOperations.FixedTimeEquals`. Lives in a pure Core
  module (`PasswordHashing.fs`) with no external dependencies.
- **Claim model:** on login, `SignInAsync` issues a cookie carrying `NameIdentifier` (member Id),
  `Name`, and `Role=Admin` (for admin members). All app routes are wrapped by `protect` / `protectAdmin`
  combinators; `loginPage`, `loginMember`, `loginSubmit`, and `logout` are anonymous.
- **First-login claim:** members with `PasswordHash = None` are "unclaimed". On first login they
  set their own password (no admin intervention needed). The seeded `Me` admin simply claims on
  first run.
- **Strict per-member isolation:** everyone (including admins) sees and records only their own
  readings. No on-behalf-of, no profile picker. Admins can manage other members via `/members`
  but cannot view their readings.
- **Schema migration:** `PasswordHash TEXT NOT NULL DEFAULT ''` added to the `Members` table via
  the existing `addColumnIfMissing` pattern. Empty string maps to `None` in F# (unclaimed).
- **`POST /members/switch` removed.** The `bp_member` cookie is no longer set or read.
- **Password reset:** admins can reset any member's password to unclaimed via
  `POST /members/{id}/reset-password`. The member must claim again on next login.

## Alternatives considered

- **Single shared gate:** one household password protects the whole app; the existing cookie
  switcher is kept. Rejected because it provides no privacy between family members.
- **Reverse-proxy auth (Authelia / Tailscale):** zero app code, but depends on deployment
  infrastructure and is not portable. Rejected in favour of a self-contained solution.
- **Admin sets passwords:** more controlled initial setup. Rejected in favour of the lower-friction
  first-login claim flow.
- **Admin on-behalf-of:** admins can view/record for other members. Rejected to keep the model
  simple — maximum privacy, no special-case paths.

## Consequences

- `FamilyMember` gains a `PasswordHash: string option` field; existing DB rows get an empty
  string (unclaimed) on first run after upgrade.
- `Views.readingForm` and `Views.memberForm` gain a `memberName: string` parameter for the nav
  logout display. Handler test helpers updated accordingly.
- `TestHost.context` and variants now set `ctx.User` to an authenticated `ClaimsPrincipal` so
  handler tests continue to exercise the full auth path without a running host.
- The `bp_member` cookie is gone. Existing running instances will drop to the login page on next
  page load after upgrade; users claim their account once and then use normal login.
