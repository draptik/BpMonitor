namespace BpMonitor.Web

/// Navigation targets used in redirects and links — single source of truth.
module Routes =
  let home = "/"
  let login = "/login"
  let logout = "/logout"
  let add = "/add"
  let history = "/history"
  let trends = "/trends"
  let readings = "/readings"
  let members = "/members"
  let recent = "/recent"
  let recentFull = "/recent/full"
  let exportJson = "/export"
  let exportCsv = "/export.csv"
  let settings = "/settings"
  let settingsLanguage = "/settings/language"
  let medications = "/medications"
  let health = "/health"

  /// Pages hosting a readings table, where a reading edit may return to (Trends also with a `trendsPeriod`).
  let readingReturnTargets = [ history; recent; trends ]

  // Parametric URL builders for id-scoped resources.
  /// `returnTo` is the page that save/cancel lead back to.
  let readingEdit (id: int) (returnTo: string) =
    $"/readings/{id}/edit?{FormFields.returnTo}={System.Uri.EscapeDataString returnTo}"

  let readingUpdate (id: int) = $"/readings/{id}"
  let memberEdit (id: int) = $"/members/{id}/edit"
  let memberUpdate (id: int) = $"/members/{id}"
  let memberResetPassword (id: int) = $"/members/{id}/reset-password"
  let medicationEdit (id: int) = $"/medications/{id}/edit"
  let medicationUpdate (id: int) = $"/medications/{id}"
  let medicationDelete (id: int) = $"/medications/{id}/delete"
  let loginMember (id: int) = $"{login}/{id}"
  let trendsGran (gran: string) = $"{trends}/{gran}"
  let trendsGranKey (gran: string) (key: string) = $"{trends}/{gran}/{key}"

  /// Query parameter names of the full Trends page.
  let trendsGranParam = "gran"
  let trendsKeyParam = "key"

  /// The full Trends page opened on one period (the `/trends/{gran}/{key}` routes are fragments).
  let trendsPeriod (gran: BpMonitor.Core.Granularity) (key: string) =
    $"{trends}?{trendsGranParam}={BpMonitor.Core.TrendPeriod.slug gran}&{trendsKeyParam}={System.Uri.EscapeDataString key}"
