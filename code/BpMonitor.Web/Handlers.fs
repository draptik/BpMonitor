namespace BpMonitor.Web

open System
open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Charts
open BpMonitor.Export

/// Falco HttpHandlers. Each resolves the per-request scoped repository from DI,
/// reuses Core validation, and renders Falco.Markup views.
module Handlers =
  let private repo (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<IReadingRepository>()

  let private memberRepo (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let private ranges (ctx: HttpContext) =
    Config.readRanges (ctx.RequestServices.GetRequiredService<IConfiguration>())

  let private timeProvider (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<TimeProvider>()

  let private logger (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BpMonitor.Web.Handlers")

  let private htmlResponse (node: XmlNode) (ctx: HttpContext) : Task =
    ctx.Response.ContentType <- "text/html; charset=utf-8"
    ctx.Response.WriteAsync(renderHtml node)

  let private badRequest (ctx: HttpContext) : Task =
    ctx.Response.StatusCode <- 400
    ctx.Response.WriteAsync("Bad request")

  let private notFound (ctx: HttpContext) : Task =
    ctx.Response.StatusCode <- 404
    ctx.Response.WriteAsync("Not found")

  let private forbidden (ctx: HttpContext) : Task =
    ctx.Response.StatusCode <- 403
    ctx.Response.WriteAsync("Forbidden")

  let private routeInt (ctx: HttpContext) (key: string) : int option =
    match ctx.Request.RouteValues.TryGetValue key with
    | true, v ->
      match Int32.TryParse(string v) with
      | true, n -> Some n
      | _ -> None
    | _ -> None

  let private routeStr (ctx: HttpContext) (key: string) : string option =
    match ctx.Request.RouteValues.TryGetValue key with
    | true, v ->
      let s = string v

      if String.IsNullOrEmpty s then None else Some s
    | _ -> None

  let private formModel (ctx: HttpContext) : Task<Binding.FormModel> =
    task {
      let! form = ctx.Request.ReadFormAsync()

      let get (k: string) =
        match form.TryGetValue k with
        | true, v -> v.ToString()
        | _ -> ""

      return
        { Binding.Systolic = get "Systolic"
          Binding.Diastolic = get "Diastolic"
          Binding.HeartRate = get "HeartRate"
          Binding.Timestamp = get "Timestamp"
          Binding.Comments = get "Comments" }
    }

  // ---------------------------------------------------------------------------
  // Auth: resolve identity from the authenticated principal
  // ---------------------------------------------------------------------------

  /// Resolves the authenticated member from the principal's NameIdentifier claim.
  /// Only valid inside a `protect`ed route — the principal is guaranteed to be present.
  let private authenticatedMember (ctx: HttpContext) : FamilyMember option =
    let claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)

    if claim = null then
      None
    else
      match Int32.TryParse(claim.Value) with
      | true, id -> (memberRepo ctx).GetById(id)
      | _ -> None

  /// Builds the auth claims principal for a member.
  let private claimsPrincipal (m: FamilyMember) : ClaimsPrincipal =
    let claims =
      [ yield Claim(ClaimTypes.NameIdentifier, string m.Id)
        yield Claim(ClaimTypes.Name, m.Name)
        if m.IsAdmin then
          yield Claim(ClaimTypes.Role, "Admin") ]

    let identity =
      ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)

    ClaimsPrincipal(identity)

  // ---------------------------------------------------------------------------
  // Auth combinators
  // ---------------------------------------------------------------------------

  /// Wraps a handler so it requires an authenticated user. Unauthenticated
  /// requests are redirected to /login.
  let protect (handler: HttpContext -> Task) : HttpContext -> Task =
    fun ctx ->
      if ctx.User.Identity <> null && ctx.User.Identity.IsAuthenticated then
        handler ctx
      else
        ctx.Response.Redirect(Routes.login)
        Task.CompletedTask

  /// Like `protect` but additionally requires the Admin role. Non-admin
  /// authenticated requests get a 403.
  let protectAdmin (handler: HttpContext -> Task) : HttpContext -> Task =
    fun ctx ->
      if ctx.User.Identity = null || not ctx.User.Identity.IsAuthenticated then
        ctx.Response.Redirect(Routes.login)
        Task.CompletedTask
      elif not (ctx.User.IsInRole("Admin")) then
        forbidden ctx
      else
        handler ctx

  /// Resolves the authenticated member and passes it to `handler`. If the member
  /// cannot be resolved (e.g. stale principal after account removal) redirects to
  /// /login instead of throwing. Mirrors `protect` but hands the member to the handler.
  let private withMember (handler: FamilyMember -> HttpContext -> Task) : HttpContext -> Task =
    fun ctx ->
      match authenticatedMember ctx with
      | None ->
        ctx.Response.Redirect(Routes.login)
        Task.CompletedTask
      | Some m -> handler m ctx

  // ---------------------------------------------------------------------------
  // Reading helpers
  // ---------------------------------------------------------------------------

  let private sortedReadings (memberId: int) (ctx: HttpContext) =
    (repo ctx).GetAll(memberId) |> List.sortByDescending _.Timestamp

  /// Renders the add/edit form after a failed submit (status 422).
  let private renderFormErrors (ctx: HttpContext) active memberName isAdmin title action errors model : Task =
    ctx.Response.StatusCode <- 422
    htmlResponse (ReadingViews.readingForm active memberName isAdmin title action errors model) ctx

  /// Validates a submitted form and persists via `save`; on any error re-renders
  /// the form with messages. Shared by create and update.
  let private submit
    (ctx: HttpContext)
    active
    memberName
    isAdmin
    title
    action
    (save: BloodPressureReading -> unit)
    : Task =
    task {
      let log = logger ctx
      let! model = formModel ctx
      let rg = ranges ctx

      match Binding.toUnvalidated model with
      | Error errorMessages ->
        log.LogWarning("Reading form validation failed (binding): {Errors}", errorMessages)
        do! renderFormErrors ctx active memberName isAdmin title action errorMessages model
      | Ok unvalidated ->
        match BloodPressureReading.parse rg unvalidated with
        | Ok reading ->
          save reading

          log.LogInformation(
            "Saved reading — systolic={Systolic} diastolic={Diastolic} heartRate={HeartRate} timestamp={Timestamp}",
            reading.Systolic,
            reading.Diastolic,
            reading.HeartRate,
            reading.Timestamp
          )

          ctx.Response.Redirect Routes.history
        | Error errors ->
          let messages = Config.formatValidationErrors rg errors
          log.LogWarning("Reading form validation failed (domain): {Errors}", messages)
          do! renderFormErrors ctx active memberName isAdmin title action messages model
    }
    :> Task

  // ---------------------------------------------------------------------------
  // Login / logout
  // ---------------------------------------------------------------------------

  let loginPage: HttpContext -> Task =
    fun ctx -> htmlResponse (LoginViews.loginPage []) ctx

  // `onFailure` lets callers choose what to render on a bad password (loginPage vs loginMember).
  let private claimedLogin
    (m: FamilyMember)
    (password: string)
    (hash: string)
    (onFailure: XmlNode)
    (ctx: HttpContext)
    : Task =
    task {
      let log = logger ctx

      if PasswordHashing.verify password hash then
        log.LogInformation("Member {Name} (Id={Id}) logged in", m.Name, m.Id)
        do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal m)
        ctx.Response.Redirect Routes.home
      else
        log.LogWarning("Failed login attempt for member {Name} (Id={Id})", m.Name, m.Id)
        ctx.Response.StatusCode <- 401
        do! htmlResponse onFailure ctx
    }
    :> Task

  let private unclaimedLogin (m: FamilyMember) (password: string) (confirm: string) (ctx: HttpContext) : Task =
    task {
      let log = logger ctx

      if String.IsNullOrWhiteSpace(password) then
        ctx.Response.StatusCode <- 422
        do! htmlResponse (LoginViews.loginMember m [ "Password cannot be empty" ]) ctx
      elif password <> confirm then
        ctx.Response.StatusCode <- 422
        do! htmlResponse (LoginViews.loginMember m [ "Passwords do not match" ]) ctx
      else
        let hashed = PasswordHashing.hash password
        let claimed = { m with PasswordHash = Some hashed }
        (memberRepo ctx).Update(claimed)
        log.LogInformation("Member {Name} (Id={Id}) claimed their account", m.Name, m.Id)
        do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal claimed)
        ctx.Response.Redirect Routes.home
    }
    :> Task

  let loginWithCredentials: HttpContext -> Task =
    fun ctx ->
      task {
        let! form = ctx.Request.ReadFormAsync()
        let username = form["Username"].ToString().Trim()
        let password = form["Password"].ToString()

        let found =
          (memberRepo ctx).GetAll()
          |> List.tryFind (fun m -> m.IsActive && m.Name.Equals(username, StringComparison.OrdinalIgnoreCase))

        match found with
        | None ->
          ctx.Response.StatusCode <- 401
          do! htmlResponse (LoginViews.loginPage [ "Invalid name or password" ]) ctx
        | Some m ->
          match m.PasswordHash with
          | Some hash -> do! claimedLogin m password hash (LoginViews.loginPage [ "Invalid name or password" ]) ctx
          | None ->
            // Unclaimed: redirect to per-member claim page
            ctx.Response.Redirect $"{Routes.login}/{m.Id}"
      }
      :> Task

  let loginMember: HttpContext -> Task =
    fun ctx ->
      match routeInt ctx "id" with
      | None -> badRequest ctx
      | Some id ->
        match (memberRepo ctx).GetById(id) with
        | None -> notFound ctx
        | Some m ->
          if not m.IsActive then
            ctx.Response.StatusCode <- 403
            ctx.Response.WriteAsync("This account is inactive")
          else
            htmlResponse (LoginViews.loginMember m []) ctx

  let loginSubmit: HttpContext -> Task =
    fun ctx ->
      task {
        match routeInt ctx "id" with
        | None -> do! badRequest ctx
        | Some id ->
          match (memberRepo ctx).GetById(id) with
          | None -> do! notFound ctx
          | Some m when not m.IsActive ->
            ctx.Response.StatusCode <- 403
            do! ctx.Response.WriteAsync("This account is inactive")
          | Some m ->
            let! form = ctx.Request.ReadFormAsync()
            let password = form["Password"].ToString()

            match m.PasswordHash with
            | Some hash -> do! claimedLogin m password hash (LoginViews.loginMember m [ "Incorrect password" ]) ctx
            | None -> do! unclaimedLogin m password (form["PasswordConfirm"].ToString()) ctx
      }
      :> Task

  let logout: HttpContext -> Task =
    fun ctx ->
      task {
        do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
        ctx.Response.Redirect Routes.login
      }
      :> Task

  // ---------------------------------------------------------------------------
  // App pages (all protected — authenticated member resolved from principal)
  // ---------------------------------------------------------------------------

  let landing: HttpContext -> Task =
    withMember (fun m ctx -> htmlResponse (ReadingViews.landing m) ctx)

  let history: HttpContext -> Task =
    withMember (fun m ctx -> htmlResponse (ReadingViews.history m (sortedReadings m.Id ctx)) ctx)

  let chart: HttpContext -> Task =
    withMember (fun m ctx ->
      let allReadings = (repo ctx).GetAll(m.Id)

      let theme =
        match ctx.Request.Query.TryGetValue "theme" with
        | true, v when string v = "dark" -> Dark
        | _ -> Light

      let granStr =
        match ctx.Request.Query.TryGetValue "gran" with
        | true, v -> string v
        | _ -> ""

      let periodStr =
        match ctx.Request.Query.TryGetValue "period" with
        | true, v -> string v
        | _ -> ""

      let html =
        match TrendPeriod.parseGranularity granStr with
        | Some gran ->
          let now = (timeProvider ctx).GetUtcNow()

          let period =
            TrendPeriod.ofKey gran periodStr now
            |> Option.defaultWith (fun () -> TrendPeriod.current gran now)

          let windowed = allReadings |> ReadingStats.between period.Start period.EndExclusive
          BpChart.toHtmlDashed gran theme (ReadingStats.aggregate gran windowed)
        | None -> BpChart.toHtml theme allReadings

      ctx.Response.ContentType <- "text/html; charset=utf-8"
      ctx.Response.WriteAsync html)

  let trends: HttpContext -> Task =
    withMember (fun m ctx ->
      let now = (timeProvider ctx).GetUtcNow()
      let allReadings = (repo ctx).GetAll(m.Id)
      let period = TrendPeriod.current Weekly now
      let windowed = allReadings |> ReadingStats.between period.Start period.EndExclusive
      let summary = ReadingStats.summarizeRange period windowed
      let periods = TrendPeriod.available Weekly now
      let tableReadings = windowed |> List.sortByDescending _.Timestamp
      htmlResponse (TrendViews.trends m summary periods tableReadings) ctx)

  let trendsPanel: HttpContext -> Task =
    withMember (fun m ctx ->
      match routeStr ctx "gran" |> Option.bind TrendPeriod.parseGranularity with
      | None -> badRequest ctx
      | Some gran ->
        let now = (timeProvider ctx).GetUtcNow()
        let allReadings = (repo ctx).GetAll(m.Id)

        let period =
          routeStr ctx "key"
          |> Option.bind (fun k -> TrendPeriod.ofKey gran k now)
          |> Option.defaultWith (fun () -> TrendPeriod.current gran now)

        let windowed = allReadings |> ReadingStats.between period.Start period.EndExclusive
        let summary = ReadingStats.summarizeRange period windowed
        let periods = TrendPeriod.available gran now
        let tableReadings = windowed |> List.sortByDescending _.Timestamp

        htmlResponse (TrendViews.trendsPanel summary periods tableReadings) ctx)

  let newReading: HttpContext -> Task =
    fun ctx ->
      let m = authenticatedMember ctx

      let prefill =
        { Binding.empty with
            Binding.Timestamp = (timeProvider ctx).GetLocalNow().ToString(Formats.timestamp) }

      htmlResponse
        (ReadingViews.readingForm
          Routes.add
          (m |> Option.map _.Name |> Option.defaultValue "")
          (m |> Option.exists _.IsAdmin)
          "Add reading"
          Routes.readings
          []
          prefill)
        ctx

  let createReading: HttpContext -> Task =
    withMember (fun m ctx -> submit ctx Routes.add m.Name m.IsAdmin "Add reading" Routes.readings ((repo ctx).Add m.Id))

  let editReading: HttpContext -> Task =
    withMember (fun m ctx ->
      let log = logger ctx

      match routeInt ctx "id" with
      | None ->
        log.LogWarning(
          "editReading: bad route value for {RouteId}",
          routeStr ctx "id" |> Option.defaultValue "<missing>"
        )

        badRequest ctx
      | Some id ->
        match (repo ctx).GetAll(m.Id) |> List.tryFind (fun r -> r.Id = id) with
        | Some r ->
          htmlResponse
            (ReadingViews.readingForm "" m.Name m.IsAdmin "Edit reading" $"/readings/{id}" [] (Binding.ofReading r))
            ctx
        | None ->
          log.LogWarning("editReading: reading {Id} not found for member {MemberId}", id, m.Id)
          notFound ctx)

  let updateReading: HttpContext -> Task =
    withMember (fun m ctx ->
      let log = logger ctx

      match routeInt ctx "id" with
      | None ->
        log.LogWarning(
          "updateReading: bad route value for {RouteId}",
          routeStr ctx "id" |> Option.defaultValue "<missing>"
        )

        badRequest ctx
      | Some id ->
        submit ctx "" m.Name m.IsAdmin "Edit reading" $"/readings/{id}" (fun r ->
          (repo ctx).Update { r with Id = id; MemberId = m.Id }))

  let exportJson: HttpContext -> Task =
    withMember (fun m ctx ->
      let json = JsonExport.serialize ((repo ctx).GetAll(m.Id))
      ctx.Response.ContentType <- "application/json; charset=utf-8"
      ctx.Response.Headers.Append("Content-Disposition", "attachment; filename=\"bpmonitor-export.json\"")
      ctx.Response.WriteAsync json)

  let exportCsv: HttpContext -> Task =
    withMember (fun m ctx ->
      let csv = CsvExport.serialize ((repo ctx).GetAll(m.Id))
      ctx.Response.ContentType <- "text/csv; charset=utf-8"
      ctx.Response.Headers.Append("Content-Disposition", "attachment; filename=\"bpmonitor-export.csv\"")
      ctx.Response.WriteAsync csv)

  // ---------------------------------------------------------------------------
  // Member management (all protectAdmin — must be admin)
  // ---------------------------------------------------------------------------

  let members: HttpContext -> Task =
    withMember (fun active ctx ->
      let allMembers = (memberRepo ctx).GetAll()
      htmlResponse (MemberViews.members allMembers active) ctx)

  let createMember: HttpContext -> Task =
    withMember (fun active ctx ->
      task {
        let! form = ctx.Request.ReadFormAsync()
        let name = form["Name"].ToString()
        let isAdmin = form.ContainsKey("IsAdmin")

        match FamilyMember.create name isAdmin with
        | Error NameIsEmpty ->
          let allMembers = (memberRepo ctx).GetAll()
          ctx.Response.StatusCode <- 422
          do! htmlResponse (MemberViews.membersWithError allMembers active "Name cannot be empty") ctx
        | Ok m ->
          (memberRepo ctx).Add(m) |> ignore
          ctx.Response.Redirect Routes.members
      }
      :> Task)

  let editMember: HttpContext -> Task =
    fun ctx ->
      let log = logger ctx

      let adminName =
        authenticatedMember ctx |> Option.map _.Name |> Option.defaultValue ""

      match routeInt ctx "id" with
      | None ->
        log.LogWarning(
          "editMember: bad route value for {RouteId}",
          routeStr ctx "id" |> Option.defaultValue "<missing>"
        )

        badRequest ctx
      | Some id ->
        match (memberRepo ctx).GetById(id) with
        | Some m ->
          htmlResponse (MemberViews.memberForm Routes.members adminName true "Edit member" $"/members/{id}" [] m) ctx
        | None ->
          log.LogWarning("editMember: member {Id} not found", id)
          notFound ctx

  let private renderMemberEditError
    (id: int)
    (adminName: string)
    (errors: string list)
    (m: FamilyMember)
    (ctx: HttpContext)
    : Task =
    ctx.Response.StatusCode <- 422
    htmlResponse (MemberViews.memberForm Routes.members adminName true "Edit member" $"/members/{id}" errors m) ctx

  let private applyMemberEdit
    (id: int)
    (adminName: string)
    (existing: FamilyMember)
    (name: string)
    (isAdmin: bool)
    (isActive: bool)
    (ctx: HttpContext)
    : Task =
    task {
      match FamilyMember.create name isAdmin with
      | Error NameIsEmpty ->
        let m =
          { existing with
              Name = ""
              IsAdmin = isAdmin
              IsActive = isActive }

        do! renderMemberEditError id adminName [ "Name cannot be empty" ] m ctx
      | Ok _ ->
        let updated =
          { existing with
              Name = name.Trim()
              IsAdmin = isAdmin
              IsActive = isActive }
        // Compute what the member list would look like after the edit.
        let postEditList =
          (memberRepo ctx).GetAll()
          |> List.map (fun m -> if m.Id = id then updated else m)

        if not (FamilyMember.hasActiveAdmin postEditList) then
          do! renderMemberEditError id adminName [ "At least one member must be an active admin" ] updated ctx
        else
          (memberRepo ctx).Update(updated)
          ctx.Response.Redirect Routes.members
    }
    :> Task

  let updateMember: HttpContext -> Task =
    fun ctx ->
      task {
        let log = logger ctx

        let adminName =
          authenticatedMember ctx |> Option.map _.Name |> Option.defaultValue ""

        match routeInt ctx "id" with
        | None ->
          log.LogWarning(
            "updateMember: bad route value for {RouteId}",
            routeStr ctx "id" |> Option.defaultValue "<missing>"
          )

          do! badRequest ctx
        | Some id ->
          match (memberRepo ctx).GetById(id) with
          | None ->
            log.LogWarning("updateMember: member {Id} not found", id)
            do! notFound ctx
          | Some existing ->
            let! form = ctx.Request.ReadFormAsync()

            do!
              applyMemberEdit
                id
                adminName
                existing
                (form["Name"].ToString())
                (form.ContainsKey("IsAdmin"))
                (form.ContainsKey("IsActive"))
                ctx
      }
      :> Task

  /// Resets a member's password to unclaimed (admin-only).
  let resetPassword: HttpContext -> Task =
    fun ctx ->
      task {
        let log = logger ctx

        match routeInt ctx "id" with
        | None ->
          log.LogWarning(
            "resetPassword: bad route value for {RouteId}",
            routeStr ctx "id" |> Option.defaultValue "<missing>"
          )

          do! badRequest ctx
        | Some id ->
          match (memberRepo ctx).GetById(id) with
          | None ->
            log.LogWarning("resetPassword: member {Id} not found", id)
            do! notFound ctx
          | Some m ->
            (memberRepo ctx).Update({ m with PasswordHash = None })
            log.LogInformation("Admin reset password for member {Name} (Id={Id})", m.Name, m.Id)
            ctx.Response.Redirect Routes.members
      }
      :> Task
