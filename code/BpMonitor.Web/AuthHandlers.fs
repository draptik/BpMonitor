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
  // ── Auth: resolve identity from the authenticated principal ──

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

  /// Returns the authenticated member's name, or "" if unauthenticated.
  let authenticatedMemberName (ctx: HttpContext) : string =
    authenticatedMember ctx |> Option.map _.Name |> Option.defaultValue ""

  /// LocalizedStrings in the authenticated member's language, falling back to `strings ctx`
  /// (cookie/Accept-Language/config) if unauthenticated.
  let authenticatedStrings (ctx: HttpContext) : LocalizedStrings =
    match authenticatedMember ctx with
    | Some m -> LocalizedStrings.forLanguage m.Language
    | None -> strings ctx

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

  // ── Auth combinators ──

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
  /// cannot be resolved (e.g., stale principal after account removal), redirects to
  /// /login instead of throwing. Mirrors `protect` but hands the member to the handler.
  let withMember (handler: FamilyMember -> HttpContext -> Task) : HttpContext -> Task =
    fun ctx ->
      match authenticatedMember ctx with
      | None ->
        ctx.Response.Redirect(Routes.login)
        Task.CompletedTask
      | Some m -> handler m ctx

  /// Resolves both the authenticated member and the "id" route segment, passing both to
  /// `handler`. Redirects to /login if the member cannot be resolved; returns 400 for a
  /// noninteger id.
  let withMemberAndRouteId
    (handlerName: string)
    (handler: FamilyMember -> int -> HttpContext -> Task)
    : HttpContext -> Task =
    withMember (fun m ctx -> (withRouteId handlerName (fun id ctx -> handler m id ctx)) ctx)

  // ── Login / logout ──

  let loginPage: HttpContext -> Task =
    fun ctx -> htmlResponse (LoginViews.loginPage (strings ctx) []) ctx

  /// `onFailure` lets callers choose what to render on a bad password (loginPage vs. loginMember).
  let private claimedLogin
    (m: FamilyMember)
    (password: string)
    (hash: string)
    (rememberMe: bool)
    (onFailure: LocalizedStrings -> XmlNode)
    (ctx: HttpContext)
    : Task =
    task {
      let log = logger ctx

      if PasswordHashing.verify password hash then
        log.LogInformation("Member {Name} (Id={Id}) logged in", m.Name, m.Id)
        setLanguageCookie ctx m.Language

        do!
          ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            claimsPrincipal m,
            AuthenticationProperties(IsPersistent = rememberMe)
          )

        ctx.Response.Redirect Routes.home
      else
        log.LogWarning("Failed login attempt for member {Name} (Id={Id})", m.Name, m.Id)
        ctx.Response.StatusCode <- 401
        do! htmlResponse (onFailure (strings ctx)) ctx
    }
    :> Task

  let private unclaimedLogin
    (m: FamilyMember)
    (password: string)
    (confirm: string)
    (rememberMe: bool)
    (ctx: HttpContext)
    : Task =
    task {
      let log = logger ctx
      let s = strings ctx

      if String.IsNullOrWhiteSpace(password) then
        ctx.Response.StatusCode <- 422
        do! htmlResponse (LoginViews.loginMember s m [ s.Login.PasswordCannotBeEmpty ]) ctx
      elif password <> confirm then
        ctx.Response.StatusCode <- 422
        do! htmlResponse (LoginViews.loginMember s m [ s.Login.PasswordsDoNotMatch ]) ctx
      else
        let hashed = PasswordHashing.hash password
        let claimed = { m with PasswordHash = Some hashed }
        (memberRepo ctx).Update(claimed)
        log.LogInformation("Member {Name} (Id={Id}) claimed their account", m.Name, m.Id)
        setLanguageCookie ctx claimed.Language

        do!
          ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            claimsPrincipal claimed,
            AuthenticationProperties(IsPersistent = rememberMe)
          )

        ctx.Response.Redirect Routes.home
    }
    :> Task

  let loginWithCredentials: HttpContext -> Task =
    fun ctx ->
      task {
        let s = strings ctx
        let! form = ctx.Request.ReadFormAsync()
        let username = form[FormFields.username].ToString().Trim()
        let password = form[FormFields.password].ToString()
        let rememberMe = form.ContainsKey(FormFields.rememberMe)

        let found =
          (memberRepo ctx).GetAll()
          |> List.tryFind (fun m -> m.IsActive && m.Name.Equals(username, StringComparison.OrdinalIgnoreCase))

        match found with
        | None ->
          ctx.Response.StatusCode <- 401
          do! htmlResponse (LoginViews.loginPage s [ s.Login.InvalidNameOrPassword ]) ctx
        | Some m ->
          match m.PasswordHash with
          | Some hash ->
            do!
              claimedLogin
                m
                password
                hash
                rememberMe
                (fun s -> LoginViews.loginPage s [ s.Login.InvalidNameOrPassword ])
                ctx
          | None ->
            // Unclaimed: redirect to per-member claim page
            ctx.Response.Redirect(Routes.loginMember m.Id)
      }
      :> Task

  let loginMember: HttpContext -> Task =
    withRouteMember "loginMember" (fun m ctx ->
      let s = strings ctx

      if not m.IsActive then
        ctx.Response.StatusCode <- 403
        ctx.Response.WriteAsync(s.Login.AccountInactive)
      else
        htmlResponse (LoginViews.loginMember s m []) ctx)

  let loginSubmit: HttpContext -> Task =
    withRouteMember "loginSubmit" (fun m ctx ->
      task {
        let s = strings ctx

        if not m.IsActive then
          ctx.Response.StatusCode <- 403
          do! ctx.Response.WriteAsync(s.Login.AccountInactive)
        else
          let! form = ctx.Request.ReadFormAsync()
          let password = form[FormFields.password].ToString()
          let rememberMe = form.ContainsKey(FormFields.rememberMe)

          match m.PasswordHash with
          | Some hash ->
            do!
              claimedLogin
                m
                password
                hash
                rememberMe
                (fun s -> LoginViews.loginMember s m [ s.Login.IncorrectPassword ])
                ctx
          | None -> do! unclaimedLogin m password (form[FormFields.passwordConfirm].ToString()) rememberMe ctx
      }
      :> Task)

  let logout: HttpContext -> Task =
    fun ctx ->
      task {
        do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
        ctx.Response.Redirect Routes.login
      }
      :> Task
