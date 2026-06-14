namespace BpMonitor.Web

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open BpMonitor.Core
open HandlerHelpers
open AuthHandlers

/// Handlers for family-member management (admin-only pages).
module MemberHandlers =
  let members: HttpContext -> Task =
    withMember (fun active ctx ->
      let allMembers = (memberRepo ctx).GetAll()
      htmlResponse (MemberViews.members allMembers active None) ctx)

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
          do! htmlResponse (MemberViews.members allMembers active (Some "Name cannot be empty")) ctx
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
