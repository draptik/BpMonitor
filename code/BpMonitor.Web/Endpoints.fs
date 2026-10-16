namespace BpMonitor.Web

open Falco
open Falco.Routing

/// The app's route table, with each route's auth wrapper (protect / protectAdmin).
module Endpoints =
  let all: HttpEndpoint list =
    [ // Anonymous: health probe
      get Routes.health HealthHandlers.health
      // Anonymous: login/logout
      get Routes.login AuthHandlers.loginPage
      post Routes.login AuthHandlers.loginWithCredentials
      get "/login/{id:int}" AuthHandlers.loginMember
      post "/login/{id:int}" AuthHandlers.loginSubmit
      post Routes.logout AuthHandlers.logout
      // Authenticated: reading CRUD + app pages
      get Routes.home (AuthHandlers.protect ReadingHandlers.landing)
      get Routes.add (AuthHandlers.protect ReadingHandlers.newReading)
      get Routes.history (AuthHandlers.protect ReadingHandlers.history)
      get Routes.recent (AuthHandlers.protect ReadingHandlers.recent)
      get Routes.recentFull (AuthHandlers.protect ReadingHandlers.recentFull)
      get Routes.trends (AuthHandlers.protect ReadingHandlers.trends)
      get "/trends/{gran}" (AuthHandlers.protect ReadingHandlers.trendsPanel)
      get "/trends/{gran}/{key}" (AuthHandlers.protect ReadingHandlers.trendsPanel)
      get Routes.exportJson (AuthHandlers.protect ReadingHandlers.exportJson)
      get Routes.exportCsv (AuthHandlers.protect ReadingHandlers.exportCsv)
      get Routes.settings (AuthHandlers.protect ReadingHandlers.settings)
      post Routes.settings (AuthHandlers.protect ReadingHandlers.updateSettings)
      post Routes.settingsLanguage (AuthHandlers.protect ReadingHandlers.updateLanguage)
      post Routes.readings (AuthHandlers.protect ReadingHandlers.createReading)
      get "/readings/{id:int}/edit" (AuthHandlers.protect ReadingHandlers.editReading)
      post "/readings/{id:int}" (AuthHandlers.protect ReadingHandlers.updateReading)
      // Authenticated: medication CRUD (self-service, on /settings)
      post Routes.medications (AuthHandlers.protect MedicationHandlers.create)
      get "/medications/{id:int}/edit" (AuthHandlers.protect MedicationHandlers.edit)
      post "/medications/{id:int}" (AuthHandlers.protect MedicationHandlers.update)
      post "/medications/{id:int}/delete" (AuthHandlers.protect MedicationHandlers.delete)
      // Admin-only: member management
      get Routes.members (AuthHandlers.protectAdmin MemberHandlers.members)
      post Routes.members (AuthHandlers.protectAdmin MemberHandlers.createMember)
      get "/members/{id:int}/edit" (AuthHandlers.protectAdmin MemberHandlers.editMember)
      post "/members/{id:int}" (AuthHandlers.protectAdmin MemberHandlers.updateMember)
      post "/members/{id:int}/reset-password" (AuthHandlers.protectAdmin MemberHandlers.resetPassword)
    ]
