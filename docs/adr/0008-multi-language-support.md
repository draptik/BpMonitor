# ADR 0008 — Multi-language support (English + German)

**Date:** 2026-08-23  
**Status:** Accepted

## Context

BpMonitor has been English-only since its first version: `Attr.lang "en"` is hardcoded in both
page shells, and every user-facing string (~120 of them, across nav, forms, validation
messages, and chart labels) is an inline literal at its call site. There is no `.resx`, no
`AddLocalization`/`RequestLocalizationMiddleware`, and no `Accept-Language` handling anywhere.
The app is a self-hosted single-household tool (ADR 0005), and its maintainer is German —
`Formats.dateEuropean = "dd.MM.yyyy"` already existed in Core as evidence of that pull. The goal
is to let each family member read the app in English or German, landed as two PRs — the
infrastructure, then the German translations themselves — with a third language costing one
new file going forward.

## Decision

**A typed F# `LocalizedStrings` record in `BpMonitor.Core`, not `.resx` + `IStringLocalizer`.** The app
has no template engine — views are plain F# functions building `Falco.Markup.XmlNode` — so
`IStringLocalizer`'s main advantage (`@Localizer["Key"]` inside Razor) buys nothing here; it
would just be a dictionary lookup called from F#. Against that, the record gives:

- **Compile-time completeness.** The compiler refuses to build until every language supplies
  every field. `.resx` silently renders the raw key at runtime on a missing translation.
- **Typed parameterized messages.** `SystolicOutOfRange: int -> int -> int -> string` has its
  arity and argument types checked; `loc["Key", v, min, max]` does not.
- **No dependency reaching into Core.** `LocalizedStrings`/`Language` are plain data, so
  `BpMonitor.Charts` (which may only reference Core per the Clean Architecture rules in
  `BpMonitor.Arch.Tests`) can render localized chart labels without a new package reference.

The one thing given up is translator tooling (resx editors, Crowdin-style workflows) — judged
worthless for a single-maintainer family app.

**Language stored per member**, mirroring the existing `Goal: GoalRange` precedent on
`FamilyMember`, with a `bpmonitor_lang` cookie mirror so `/login` (which has no member yet) is
also localized. Resolution order: authenticated member's `Language` → cookie →
`Accept-Language` → `BpMonitor:DefaultLanguage` config key → `English`. No
`RequestLocalizationMiddleware` — resolution stays an explicit function call
(`HandlerHelpers.strings`/`AuthHandlers.authenticatedStrings`), matching the rest of the app's
all-explicit style rather than introducing ambient ASP.NET Core culture state.

**`LocalizedStrings` passed as an explicit first parameter** to every view and handler that needs it,
not read from an ambient `CurrentUICulture` — same reasoning as the resolution order above.

**`TrendPeriod.Label` restructured from a `string` to a `PeriodLabel` DU** (`ThisWeek`,
`CalendarWeek of int`, `MonthOfYear of int * int`, …), rendered at the view layer via
`LocalizedStrings.Trend`. This removes English prose that had leaked into the domain layer, rather than
translating around it.

## Consequences

- Adding a further language is one `LocalizedStrings` record value plus one `Language` case;
  the build fails loudly until every field is supplied.
- Every view function in `BpMonitor.Web` and the four public `BpMonitor.Charts.BpChart` chart
  functions gained a `LocalizedStrings`/`ChartStrings` parameter — a mechanical but real signature change
  across ~30 functions.
- `Members` gained a `Language TEXT NOT NULL DEFAULT 'en'` column, backfilled via
  `SchemaMigrations.addColumnIfMissing` for existing databases.
- Plotly's own built-in locale strings (e.g. the x-axis spike's default date format) stay
  English until the locale bundles are vendored alongside `plotly-2.27.1.min.js` — out of scope
  here.
