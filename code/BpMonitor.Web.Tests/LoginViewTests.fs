module LoginViewTests

open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Web
open ViewTestHelpers

[<Fact>]
let ``loginPage renders sign-in form with username and password fields`` () =
  let html = renderHtml (LoginViews.loginPage [])

  test <@ html.Contains "Sign in" @>
  test <@ html.Contains "Username" @>
  test <@ html.Contains "Password" @>

[<Fact>]
let ``loginPage renders errors when provided`` () =
  let html = renderHtml (LoginViews.loginPage [ "Invalid name or password" ])

  test <@ html.Contains "Invalid name or password" @>

[<Fact>]
let ``loginMember shows claim form for unclaimed member`` () =
  let html = renderHtml (LoginViews.loginMember defaultMember [])

  test <@ html.Contains "PasswordConfirm" @>
  test <@ html.Contains "Claim account" @>

[<Fact>]
let ``loginMember shows password form for claimed member`` () =
  let claimed =
    { defaultMember with
        PasswordHash = Some "x" }

  let html = renderHtml (LoginViews.loginMember claimed [])

  test <@ not (html.Contains "PasswordConfirm") @>
  test <@ html.Contains "Login" @>

[<Fact>]
let ``loginMember renders errors`` () =
  let html =
    renderHtml (LoginViews.loginMember defaultMember [ "Passwords do not match" ])

  test <@ html.Contains "Passwords do not match" @>
