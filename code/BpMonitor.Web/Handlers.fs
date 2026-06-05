namespace BpMonitor.Web

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Charts

/// Falco HttpHandlers. Each resolves the per-request scoped repository from DI,
/// reuses Core validation, and renders Falco.Markup views.
module Handlers =
  let private repo (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<IReadingRepository>()

  let private memberRepo (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let private ranges (ctx: HttpContext) =
    Config.readRanges (ctx.RequestServices.GetRequiredService<IConfiguration>())

  let private logger (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BpMonitor.Web.Handlers")

  let private htmlResponse (node: XmlNode) (ctx: HttpContext) : Task =
    ctx.Response.ContentType <- "text/html; charset=utf-8"
    ctx.Response.WriteAsync(renderHtml node)

  let private routeInt (ctx: HttpContext) (key: string) : int option =
    match ctx.Request.RouteValues.TryGetValue key with
    | true, v ->
      match Int32.TryParse(string v) with
      | true, n -> Some n
      | _ -> None
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

  /// Resolves the active family member from the bp_member cookie, falling back to
  /// the first member when no cookie is set or the cookie Id is invalid.
  let private activeMember (ctx: HttpContext) : FamilyMember =
    let allMembers = (memberRepo ctx).GetAll()

    let cookieMemberId =
      match ctx.Request.Cookies.TryGetValue("bp_member") with
      | true, v ->
        match Int32.TryParse(v) with
        | true, id -> Some id
        | _ -> None
      | _ -> None

    cookieMemberId
    |> Option.bind (fun id -> allMembers |> List.tryFind (fun m -> m.Id = id))
    |> Option.orElse (allMembers |> List.tryHead)
    |> Option.defaultWith (fun () -> failwith "No family members configured. Visit /members to create one.")

  let private sortedReadings (memberId: int) (ctx: HttpContext) =
    (repo ctx).GetAll(memberId) |> List.sortByDescending _.Timestamp

  /// Renders the add/edit form after a failed submit (status 422).
  let private renderFormErrors (ctx: HttpContext) active title action errors model : Task =
    ctx.Response.StatusCode <- 422
    htmlResponse (Views.readingForm active title action errors model) ctx

  /// Validates a submitted form and persists via `save`; on any error re-renders
  /// the form with messages. Shared by create and update.
  let private submit (ctx: HttpContext) active title action (save: BloodPressureReading -> unit) : Task =
    task {
      let log = logger ctx
      let! model = formModel ctx
      let rg = ranges ctx

      match Binding.toUnvalidated model with
      | Error errorMessages ->
        log.LogWarning("Reading form validation failed (binding): {Errors}", errorMessages)
        do! renderFormErrors ctx active title action errorMessages model
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

          ctx.Response.Redirect "/history"
        | Error errors ->
          let messages = Config.formatValidationErrors rg errors
          log.LogWarning("Reading form validation failed (domain): {Errors}", messages)
          do! renderFormErrors ctx active title action messages model
    }
    :> Task

  let landing: HttpContext -> Task = fun ctx -> htmlResponse Views.landing ctx

  let history: HttpContext -> Task =
    fun ctx ->
      let m = activeMember ctx
      htmlResponse (Views.history m (sortedReadings m.Id ctx)) ctx

  let chart: HttpContext -> Task =
    fun ctx ->
      let m = activeMember ctx
      ctx.Response.ContentType <- "text/html; charset=utf-8"
      ctx.Response.WriteAsync(BpChart.toHtml ((repo ctx).GetAll(m.Id)))

  let newReading: HttpContext -> Task =
    fun ctx ->
      let prefill =
        { Binding.empty with
            Binding.Timestamp = DateTimeOffset.Now.ToString(Formats.timestamp) }

      htmlResponse (Views.readingForm "/add" "Add reading" "/readings" [] prefill) ctx

  let createReading: HttpContext -> Task =
    fun ctx ->
      let m = activeMember ctx
      submit ctx "/add" "Add reading" "/readings" ((repo ctx).Add m.Id)

  let editReading: HttpContext -> Task =
    fun ctx ->
      let log = logger ctx
      let m = activeMember ctx

      match routeInt ctx "id" with
      | None ->
        log.LogWarning("editReading: bad route value for {{id}}")
        ctx.Response.StatusCode <- 400
        ctx.Response.WriteAsync("Bad request")
      | Some id ->
        match (repo ctx).GetAll(m.Id) |> List.tryFind (fun r -> r.Id = id) with
        | Some r -> htmlResponse (Views.readingForm "" "Edit reading" $"/readings/{id}" [] (Binding.ofReading r)) ctx
        | None ->
          log.LogWarning("editReading: reading {Id} not found for member {MemberId}", id, m.Id)
          ctx.Response.StatusCode <- 404
          ctx.Response.WriteAsync("Not found")

  let updateReading: HttpContext -> Task =
    fun ctx ->
      let log = logger ctx
      let m = activeMember ctx

      match routeInt ctx "id" with
      | None ->
        log.LogWarning("updateReading: bad route value for {{id}}")
        ctx.Response.StatusCode <- 400
        ctx.Response.WriteAsync("Bad request")
      | Some id ->
        submit ctx "" "Edit reading" $"/readings/{id}" (fun r -> (repo ctx).Update { r with Id = id; MemberId = m.Id })

  let members: HttpContext -> Task =
    fun ctx ->
      let allMembers = (memberRepo ctx).GetAll()
      let active = activeMember ctx
      htmlResponse (Views.members allMembers active) ctx

  let createMember: HttpContext -> Task =
    fun ctx ->
      task {
        let! form = ctx.Request.ReadFormAsync()
        let name = form["Name"].ToString()
        let isAdmin = form.ContainsKey("IsAdmin")

        match FamilyMember.create name isAdmin with
        | Error NameIsEmpty ->
          let allMembers = (memberRepo ctx).GetAll()
          let active = activeMember ctx
          ctx.Response.StatusCode <- 422
          do! htmlResponse (Views.membersWithError allMembers active "Name cannot be empty") ctx
        | Ok m ->
          let created = (memberRepo ctx).Add(m)

          ctx.Response.Cookies.Append(
            "bp_member",
            string created.Id,
            CookieOptions(HttpOnly = true, SameSite = SameSiteMode.Strict)
          )

          ctx.Response.Redirect "/members"
      }
      :> Task

  let editMember: HttpContext -> Task =
    fun ctx ->
      let log = logger ctx

      match routeInt ctx "id" with
      | None ->
        log.LogWarning("editMember: bad route value for {{id}}")
        ctx.Response.StatusCode <- 400
        ctx.Response.WriteAsync("Bad request")
      | Some id ->
        match (memberRepo ctx).GetById(id) with
        | Some m -> htmlResponse (Views.memberForm "/members" "Edit member" $"/members/{id}" [] m) ctx
        | None ->
          log.LogWarning("editMember: member {Id} not found", id)
          ctx.Response.StatusCode <- 404
          ctx.Response.WriteAsync("Not found")

  let updateMember: HttpContext -> Task =
    fun ctx ->
      task {
        let log = logger ctx

        match routeInt ctx "id" with
        | None ->
          log.LogWarning("updateMember: bad route value for {{id}}")
          ctx.Response.StatusCode <- 400
          do! ctx.Response.WriteAsync("Bad request")
        | Some id ->
          match (memberRepo ctx).GetById(id) with
          | None ->
            log.LogWarning("updateMember: member {Id} not found", id)
            ctx.Response.StatusCode <- 404
            do! ctx.Response.WriteAsync("Not found")
          | Some existing ->
            let! form = ctx.Request.ReadFormAsync()
            let name = form["Name"].ToString()
            let isAdmin = form.ContainsKey("IsAdmin")
            let isActive = form.ContainsKey("IsActive")

            match FamilyMember.create name isAdmin with
            | Error NameIsEmpty ->
              let updated =
                { existing with
                    Name = ""
                    IsAdmin = isAdmin
                    IsActive = isActive }

              ctx.Response.StatusCode <- 422

              do!
                htmlResponse
                  (Views.memberForm "/members" "Edit member" $"/members/{id}" [ "Name cannot be empty" ] updated)
                  ctx
            | Ok _ ->
              let updated =
                { existing with
                    Name = name.Trim()
                    IsAdmin = isAdmin
                    IsActive = isActive }
              // Compute what the member list would look like after the edit.
              let allMembers = (memberRepo ctx).GetAll()

              let postEditList =
                allMembers |> List.map (fun m -> if m.Id = id then updated else m)

              if not (FamilyMember.hasActiveAdmin postEditList) then
                ctx.Response.StatusCode <- 422

                do!
                  htmlResponse
                    (Views.memberForm
                      "/members"
                      "Edit member"
                      $"/members/{id}"
                      [ "At least one member must be an active admin" ]
                      updated)
                    ctx
              else
                (memberRepo ctx).Update(updated)
                ctx.Response.Redirect "/members"
      }
      :> Task

  let switchMember: HttpContext -> Task =
    fun ctx ->
      task {
        let! form = ctx.Request.ReadFormAsync()
        let memberId = form["MemberId"].ToString()

        ctx.Response.Cookies.Append(
          "bp_member",
          memberId,
          CookieOptions(HttpOnly = true, SameSite = SameSiteMode.Strict)
        )

        let returnUrl =
          match form.TryGetValue("ReturnUrl") with
          | true, v when v.ToString() <> "" -> v.ToString()
          | _ -> "/"

        ctx.Response.Redirect(returnUrl)
      }
      :> Task
