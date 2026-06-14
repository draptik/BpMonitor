namespace BpMonitor.Web

open System
open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Falco.Markup
open BpMonitor.Core
open HandlerHelpers

/// Authentication combinators and login/logout handlers.
module AuthHandlers =
  // ---------------------------------------------------------------------------
  // Auth: resolve identity from the authenticated principal
  // ---------------------------------------------------------------------------

  /// Resolves the authenticated member from the principal's NameIdentifier claim.
  /// Only valid inside a `protect`ed route — the principal is guaranteed to be present.
  let authenticatedMember (ctx: HttpContext) : FamilyMember option =
    let claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)

    if claim = null then
      None
    else
      match Int32.TryParse(claim.Value) with
      | true, id -> (memberRepo ctx).GetById(id)
      | _ -> None

  /// Builds the auth claims principal for a member.
  let claimsPrincipal (m: FamilyMember) : ClaimsPrincipal =
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
  let withMember (handler: FamilyMember -> HttpContext -> Task) : HttpContext -> Task =
    fun ctx ->
      match authenticatedMember ctx with
      | None ->
        ctx.Response.Redirect(Routes.login)
        Task.CompletedTask
      | Some m -> handler m ctx

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
    withRouteMember "loginMember" (fun m ctx ->
      if not m.IsActive then
        ctx.Response.StatusCode <- 403
        ctx.Response.WriteAsync("This account is inactive")
      else
        htmlResponse (LoginViews.loginMember m []) ctx)

  let loginSubmit: HttpContext -> Task =
    withRouteMember "loginSubmit" (fun m ctx ->
      task {
        if not m.IsActive then
          ctx.Response.StatusCode <- 403
          do! ctx.Response.WriteAsync("This account is inactive")
        else
          let! form = ctx.Request.ReadFormAsync()
          let password = form["Password"].ToString()

          match m.PasswordHash with
          | Some hash -> do! claimedLogin m password hash (LoginViews.loginMember m [ "Incorrect password" ]) ctx
          | None -> do! unclaimedLogin m password (form["PasswordConfirm"].ToString()) ctx
      }
      :> Task)

  let logout: HttpContext -> Task =
    fun ctx ->
      task {
        do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
        ctx.Response.Redirect Routes.login
      }
      :> Task
