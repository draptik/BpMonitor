namespace BpMonitor.Web

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open BpMonitor.Core
open HandlerHelpers
open AuthHandlers

/// Handlers for family-member management (admin-only pages); each takes the signed-in admin.
module MemberHandlers =
  /// Gives `m` a new setup code and shows it once to the admin (ADR 0012).
  let private issueSetupCode (admin: FamilyMember) (m: FamilyMember) (ctx: HttpContext) : Task =
    let code = SetupCode.generate ()

    let updated =
      { m with
          Credential = PendingSetup(SetupCode.hash code)
      }

    (memberRepo ctx).Update updated
    ctx.Response.Headers.CacheControl <- "no-store"
    htmlResponse (MemberViews.setupCodeIssued (stringsForMember admin) admin.Name updated code) ctx

  let members (active: FamilyMember) (ctx: HttpContext) : Task =
    let allMembers = (memberRepo ctx).GetAll()
    htmlResponse (MemberViews.members (stringsForMember active) allMembers active []) ctx

  let createMember (active: FamilyMember) (ctx: HttpContext) : Task =
    task {
      let s = stringsForMember active
      let! form = ctx.Request.ReadFormAsync()
      let allMembers = (memberRepo ctx).GetAll()

      let renderError error =
        ctx.Response.StatusCode <- 422
        htmlResponse (MemberViews.members s allMembers active [ error ]) ctx

      match FamilyMember.create (form[FormFields.name].ToString()) (form.ContainsKey(FormFields.isAdmin)) with
      | Error NameIsEmpty -> do! renderError s.Errors.NameIsEmpty
      | Ok m when FamilyMember.nameTaken allMembers m -> do! renderError s.Errors.NameAlreadyTaken
      | Ok m ->
        let added = (memberRepo ctx).Add m
        do! issueSetupCode active added ctx
    }
    :> Task

  let private renderMemberForm (admin: FamilyMember) (errors: string list) (m: FamilyMember) (ctx: HttpContext) =
    let s = stringsForMember admin

    htmlResponse
      (MemberViews.memberForm
        s
        Routes.members
        admin.Name
        true
        s.Member.EditMemberTitle
        (Routes.memberUpdate m.Id)
        errors
        m)
      ctx

  let editMember (admin: FamilyMember) : HttpContext -> Task =
    withRouteMember "editMember" (renderMemberForm admin [])

  let updateMember (admin: FamilyMember) : HttpContext -> Task =
    withRouteMember "updateMember" (fun existing ctx ->
      task {
        let s = stringsForMember admin
        let! form = ctx.Request.ReadFormAsync()
        let isAdmin = form.ContainsKey(FormFields.isAdmin)
        let isActive = form.ContainsKey(FormFields.isActive)

        let renderError error edited =
          ctx.Response.StatusCode <- 422
          renderMemberForm admin [ error ] edited ctx

        match FamilyMember.create (form[FormFields.name].ToString()) isAdmin with
        | Error NameIsEmpty ->
          do!
            renderError
              s.Errors.NameIsEmpty
              { existing with
                  Name = ""
                  IsAdmin = isAdmin
                  IsActive = isActive
              }
        | Ok valid ->
          let updated =
            { existing with
                Name = valid.Name
                IsAdmin = isAdmin
                IsActive = isActive
            }

          let allMembers = (memberRepo ctx).GetAll()

          let afterEdit =
            allMembers |> List.map (fun m -> if m.Id = updated.Id then updated else m)

          if FamilyMember.nameTaken allMembers updated then
            do! renderError s.Errors.NameAlreadyTaken updated
          elif not (FamilyMember.hasActiveAdmin afterEdit) then
            do! renderError s.Errors.AtLeastOneActiveAdmin updated
          else
            (memberRepo ctx).Update(updated)
            ctx.Response.Redirect Routes.members
      }
      :> Task)

  /// Resets a member's password and issues a new setup code (admin-only).
  let resetPassword (admin: FamilyMember) : HttpContext -> Task =
    withRouteMember "resetPassword" (fun m ctx ->
      let log = logger ctx
      log.LogInformation("Admin reset password for member {Name} (Id={Id})", m.Name, m.Id)
      issueSetupCode admin m ctx)
