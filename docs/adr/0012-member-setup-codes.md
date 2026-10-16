# ADR 0012 — One-time setup codes for claiming an account

**Date:** 2026-10-16  
**Status:** Proposed  
**Supersedes:** the "First-login claim" part of ADR 0006

## Context

ADR 0006 let an unclaimed member (no password yet) set their own password on first login, with
no proof of identity. Whoever reached `/login/{id}` first owned the account. That covered every
newly created member, every member right after an admin reset their password, and the seeded
`Me` admin on a fresh install — so a fresh install or a reset admin account could be taken over,
admin rights included, by anyone who could reach the server.

## Decision

Claiming an account requires a **one-time setup code**.

- **Credential model:** `FamilyMember.PasswordHash: string option` becomes
  `Credential: Credential` with three cases:
  - `Password of hash` — claimed; normal name + password login.
  - `SetupCode of hash` — unclaimed; claiming requires the code.
  - `NoCredential` — no password and no code (a fresh `Me` row, a pre-upgrade unclaimed member,
    or a demo member). Can't be claimed; exists only until the startup step below runs.
- **Code format:** 8 characters from `ABCDEFGHJKMNPQRSTVWXYZ23456789` (no look-alikes such as
  `0`/`O` or `1`/`I`/`L`), shown as `K7QX-M2PA`; about 39 bits. Input is normalised (upper-case,
  dashes and spaces removed) before checking, so `k7qx m2pa` works.
- **Storage:** only a hash is stored, using the same PBKDF2 scheme as passwords
  (`PasswordHashing`), in a new `Members.SetupCodeHash` column (`TEXT NOT NULL DEFAULT ''`, via
  `addColumnIfMissing`). Claiming sets the password and clears the code.
- **Issuing:**
  - **Create member / reset password** (admin): the response page shows the code once
    (`Cache-Control: no-store`) instead of redirecting to `/members`.
  - **Startup:** after migrations and optional demo seeding, every `NoCredential` member gets a
    code, logged once at Information level (`Setup code for member "Me" (Id=1): K7QX-M2PA`).
    This one mechanism covers a fresh install, upgrades and demo data.
- **No expiry:** a code is valid until it's used or replaced by a reset.
- **Claim page:** asks for setup code, new password and confirmation. A wrong code returns 401.

## Alternatives considered

- **Keep the open claim flow and document the risk.** No code change, but a fresh install stays
  open until someone claims `Me`. Rejected.
- **Admin sets the initial password.** The alternative ADR 0006 rejected; the admin learns every
  member's first password, and the first admin still needs a bootstrap. Rejected.
- **Expiring codes.** More moving parts (timestamps, an "expired" state and message) for little
  gain in a household app where an admin can reset at any time. Rejected for now.

## Consequences

- Startup logs contain setup codes until they're used. Anyone with log access can claim those
  accounts; that's acceptable because log access already implies host access.
- The install guide's first-run step becomes "read the setup code for `Me` from the log".
- The E2E fixture and `scripts/generate-screenshots.fsx` read codes from the app's log output
  (for `Me` and demo members) or from the create-member response page.
- A fresh `Me` (or any `NoCredential` member) can't sign in at all until startup has issued a
  code, which happens before the app serves requests.
