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

  // Parametric URL builders for id-scoped resources.
  let readingEdit (id: int) = $"/readings/{id}/edit"
  let readingUpdate (id: int) = $"/readings/{id}"
  let memberEdit (id: int) = $"/members/{id}/edit"
  let memberUpdate (id: int) = $"/members/{id}"
  let memberResetPassword (id: int) = $"/members/{id}/reset-password"
  let loginMember (id: int) = $"{login}/{id}"
  let trendsGran (gran: string) = $"{trends}/{gran}"
  let trendsGranKey (gran: string) (key: string) = $"{trends}/{gran}/{key}"
