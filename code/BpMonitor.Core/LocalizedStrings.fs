namespace BpMonitor.Core

/// Shell chrome: nav links, topbar, page shell primitives shared by every page.
type ShellStrings =
  { AppTagline: string
    Menu: string
    Logout: string
    NavAdd: string
    NavRecent: string
    NavTrends: string
    NavHistory: string
    NavExportJson: string
    NavExportCsv: string
    NavSettings: string
    NavMembers: string
    Save: string
    Cancel: string
    Back: string
    Edit: string
    Delete: string
    Name: string
    Comment: string }

/// The shared readings table (ViewLayout.readingsTable) and its column units.
type ReadingTableStrings =
  { Timestamp: string
    Systolic: string
    Diastolic: string
    HeartRate: string
    MmHg: string
    Bpm: string }

type LoginStrings =
  { PageTitle: string
    SignIn: string
    Password: string
    RememberMe: string
    LoginAs: string -> string
    NewPassword: string
    ConfirmPassword: string
    ClaimHint: string
    ClaimAccount: string
    Login: string
    InvalidNameOrPassword: string
    IncorrectPassword: string
    AccountInactive: string
    PasswordCannotBeEmpty: string
    PasswordsDoNotMatch: string }

type ReadingPageStrings =
  { LandingTitle: string
    LandingTagline: string
    HistoryTitle: string
    BloodPressureGraph: string
    RecentTitle: string
    LoadFullHistory: string
    ChartCitationPrefix: string
    Last7Days: string
    Last30Days: string
    RecentReadingsSection: string
    AddReadingTitle: string
    EditReadingTitle: string
    TimestampHint: string }

type MemberPageStrings =
  { FamilyMembersTitle: string
    AddFamilyMember: string
    AddMember: string
    EditMemberTitle: string
    AdminHeader: string
    ActiveHeader: string
    PasswordHeader: string
    AdminBadge: string
    ActiveBadge: string
    ClaimedBadge: string
    UnclaimedBadge: string
    NoneBadge: string
    You: string
    ResetPassword: string
    AdminCheckboxLabel: string
    ActiveCheckboxLabel: string
    LanguageTitle: string
    GoalRangeTitle: string
    SystolicMin: string
    SystolicMax: string
    DiastolicMin: string
    DiastolicMax: string }

type MedicationStrings =
  { Required: string
    Optional: string
    DeleteConfirm: string -> string
    MedicationsTitle: string
    FullNameHeader: string
    StartHeader: string
    EndHeader: string
    AddMedicationTitle: string
    NameHint: string
    FullNameHint: string
    StartDateLabel: string
    StartDateHint: string
    EndDateLabel: string
    EndDateHint: string
    EditMedicationTitle: string
    MedicationsTimelineTitle: string
    NameIsEmpty: string
    EndDateBeforeStartDate: string }

type TrendStrings =
  { TrendsTitle: string
    Weekly: string
    Monthly: string
    Yearly: string
    NoReadingsIn: string -> string
    Readings: string
    AvgSystolic: string
    AvgDiastolic: string
    AvgHeartRate: string
    StatValue: int -> int -> int -> string
    ThisWeek: string
    LastWeek: string
    CalendarWeek: int -> string
    CalendarWeekOfYear: int -> int -> string
    ThisMonth: string
    LastMonth: string
    MonthOfYear: int -> int -> string
    ThisYear: string
    LastYear: string
    Year: int -> string }

type ErrorStrings =
  { NotAnInteger: string -> string -> string
    NotAValidDateTime: string -> string
    NotAValidDate: string -> string -> string
    NameIsEmpty: string
    AtLeastOneActiveAdmin: string
    SystolicMinMustBeLessThanMax: string
    DiastolicMinMustBeLessThanMax: string
    SystolicOutOfRange: int -> int -> int -> string
    DiastolicOutOfRange: int -> int -> int -> string
    HeartRateOutOfRange: int -> int -> int -> string }

type ChartStrings =
  {
    Systolic: string
    Diastolic: string
    SystolicTrend: string
    DiastolicTrend: string
    Comments: string
    Ongoing: string
    AxisTitle: string
    CalendarWeekTick: int -> string
    DayMonthTick: System.DateTime -> string
    MonthTick: System.DateTime -> string
    /// Sunday-first abbreviated weekday names, for Plotly's locale mechanism (see Charts.fs).
    ShortWeekdays: string list
  }

/// All user-facing text for one language. Every language must supply every field —
/// the compiler enforces completeness, which is the point of this type over `.resx`.
type LocalizedStrings =
  { Language: Language
    Shell: ShellStrings
    Table: ReadingTableStrings
    Login: LoginStrings
    Reading: ReadingPageStrings
    Member: MemberPageStrings
    Medication: MedicationStrings
    Trend: TrendStrings
    Errors: ErrorStrings
    Charts: ChartStrings }

module LocalizedStrings =
  let en: LocalizedStrings =
    { Language = English
      Shell =
        { AppTagline = "Blood pressure tracker"
          Menu = "Menu"
          Logout = "Logout"
          NavAdd = "Add"
          NavRecent = "Recent"
          NavTrends = "Trends"
          NavHistory = "History"
          NavExportJson = "Export JSON"
          NavExportCsv = "Export CSV"
          NavSettings = "Settings"
          NavMembers = "Members"
          Save = "Save"
          Cancel = "Cancel"
          Back = "Back"
          Edit = "Edit"
          Delete = "Delete"
          Name = "Name"
          Comment = "Comment" }
      Table =
        { Timestamp = "Timestamp"
          Systolic = "Systolic"
          Diastolic = "Diastolic"
          HeartRate = "Heart Rate"
          MmHg = "mmHg"
          Bpm = "bpm" }
      Login =
        { PageTitle = "Login"
          SignIn = "Sign in"
          Password = "Password"
          RememberMe = "Remember me on this device"
          LoginAs = fun name -> $"Login as {name}"
          NewPassword = "New password"
          ConfirmPassword = "Confirm password"
          ClaimHint = "This account hasn't been claimed yet. Choose a password to activate it."
          ClaimAccount = "Claim account"
          Login = "Login"
          InvalidNameOrPassword = "Invalid name or password"
          IncorrectPassword = "Incorrect password"
          AccountInactive = "This account is inactive"
          PasswordCannotBeEmpty = "Password cannot be empty"
          PasswordsDoNotMatch = "Passwords do not match" }
      Reading =
        { LandingTitle = "BpMonitor"
          LandingTagline = "Track and review your blood pressure readings."
          HistoryTitle = "History"
          BloodPressureGraph = "Blood Pressure Graph"
          RecentTitle = "Recent"
          LoadFullHistory = "Load full history"
          ChartCitationPrefix = "Chart layout inspired by "
          Last7Days = "Last 7 days"
          Last30Days = "Last 30 days"
          RecentReadingsSection = "Readings in view"
          AddReadingTitle = "Add reading"
          EditReadingTitle = "Edit reading"
          TimestampHint = "yyyy-MM-dd HH:mm" }
      Member =
        { FamilyMembersTitle = "Family Members"
          AddFamilyMember = "Add family member"
          AddMember = "Add member"
          EditMemberTitle = "Edit member"
          AdminHeader = "Admin"
          ActiveHeader = "Active"
          PasswordHeader = "Password"
          AdminBadge = "Admin"
          ActiveBadge = "Active"
          ClaimedBadge = "Claimed"
          UnclaimedBadge = "Unclaimed"
          NoneBadge = "—"
          You = "You"
          ResetPassword = "Reset password"
          AdminCheckboxLabel = " Admin"
          ActiveCheckboxLabel = " Active"
          LanguageTitle = "Language"
          GoalRangeTitle = "Goal Range"
          SystolicMin = "Systolic min"
          SystolicMax = "Systolic max"
          DiastolicMin = "Diastolic min"
          DiastolicMax = "Diastolic max" }
      Medication =
        { Required = "Required"
          Optional = "Optional"
          DeleteConfirm = fun name -> $"Delete {name}? This cannot be undone."
          MedicationsTitle = "Medications"
          FullNameHeader = "Full name"
          StartHeader = "Start"
          EndHeader = "End"
          AddMedicationTitle = "Add medication"
          NameHint = "Short label shown on the timeline, e.g. HCTZ"
          FullNameHint = "Long form, shown in the timeline's hover tooltip"
          StartDateLabel = "Start date"
          StartDateHint = "dd.mm.yyyy"
          EndDateLabel = "End date"
          EndDateHint = "dd.mm.yyyy"
          EditMedicationTitle = "Edit medication"
          MedicationsTimelineTitle = "Medications Timeline"
          NameIsEmpty = "Name cannot be empty"
          EndDateBeforeStartDate = "End date must be on or after the start date" }
      Trend =
        { TrendsTitle = "Trends"
          Weekly = "Weekly"
          Monthly = "Monthly"
          Yearly = "Yearly"
          NoReadingsIn = fun label -> $"No readings in {label}."
          Readings = "Readings"
          AvgSystolic = "Avg Systolic"
          AvgDiastolic = "Avg Diastolic"
          AvgHeartRate = "Avg Heart Rate"
          StatValue = fun avg mn mx -> $"{avg} (min: {mn}, max: {mx})"
          ThisWeek = "This Week"
          LastWeek = "Last Week"
          CalendarWeek = fun week -> $"CW {week}"
          CalendarWeekOfYear = fun week year -> $"CW {week}/{year}"
          ThisMonth = "This Month"
          LastMonth = "Last Month"
          MonthOfYear =
            fun month year ->
              System.DateTime(year, month, 1).ToString("MMM yyyy", System.Globalization.CultureInfo("en-US"))
          ThisYear = "This Year"
          LastYear = "Last Year"
          Year = fun year -> string year }
      Errors =
        { NotAnInteger = fun label value -> $"{label}: '{value}' is not a valid integer"
          NotAValidDateTime = fun value -> $"Timestamp: '{value}' is not a valid date/time"
          NotAValidDate = fun label value -> $"{label}: '{value}' is not a valid date (expected dd.mm.yyyy)"
          NameIsEmpty = "Name cannot be empty"
          AtLeastOneActiveAdmin = "At least one member must be an active admin"
          SystolicMinMustBeLessThanMax = "Systolic min must be less than systolic max"
          DiastolicMinMustBeLessThanMax = "Diastolic min must be less than diastolic max"
          SystolicOutOfRange = fun v lo hi -> $"Systolic {v} is out of range ({lo}–{hi})"
          DiastolicOutOfRange = fun v lo hi -> $"Diastolic {v} is out of range ({lo}–{hi})"
          HeartRateOutOfRange = fun v lo hi -> $"Heart rate {v} is out of range ({lo}–{hi})" }
      Charts =
        { Systolic = "Systolic"
          Diastolic = "Diastolic"
          SystolicTrend = "Systolic (trend)"
          DiastolicTrend = "Diastolic (trend)"
          Comments = "Comments"
          Ongoing = "ongoing"
          AxisTitle = "blood pressure [mmHg]"
          CalendarWeekTick = fun week -> $"W{week}"
          DayMonthTick = fun date -> date.ToString("d MMM", System.Globalization.CultureInfo("en-US"))
          MonthTick = fun date -> date.ToString("MMM", System.Globalization.CultureInfo("en-US"))
          ShortWeekdays =
            System.Globalization.CultureInfo("en-US").DateTimeFormat.AbbreviatedDayNames
            |> Array.toList } }

  let de: LocalizedStrings =
    { Language = German
      Shell =
        { AppTagline = "Blutdruck-Tracker"
          Menu = "Menü"
          Logout = "Abmelden"
          NavAdd = "Hinzufügen"
          NavRecent = "Aktuell"
          NavTrends = "Trends"
          NavHistory = "Verlauf"
          NavExportJson = "JSON exportieren"
          NavExportCsv = "CSV exportieren"
          NavSettings = "Einstellungen"
          NavMembers = "Mitglieder"
          Save = "Speichern"
          Cancel = "Abbrechen"
          Back = "Zurück"
          Edit = "Bearbeiten"
          Delete = "Löschen"
          Name = "Name"
          Comment = "Kommentar" }
      Table =
        { Timestamp = "Zeitstempel"
          Systolic = "Systolisch"
          Diastolic = "Diastolisch"
          HeartRate = "Herzfrequenz"
          MmHg = "mmHg"
          Bpm = "bpm" }
      Login =
        { PageTitle = "Anmeldung"
          SignIn = "Anmelden"
          Password = "Passwort"
          RememberMe = "Auf diesem Gerät angemeldet bleiben"
          LoginAs = fun name -> $"Anmelden als {name}"
          NewPassword = "Neues Passwort"
          ConfirmPassword = "Passwort bestätigen"
          ClaimHint = "Dieses Konto wurde noch nicht aktiviert. Wähle ein Passwort, um es zu aktivieren."
          ClaimAccount = "Konto aktivieren"
          Login = "Anmelden"
          InvalidNameOrPassword = "Ungültiger Name oder ungültiges Passwort"
          IncorrectPassword = "Falsches Passwort"
          AccountInactive = "Dieses Konto ist inaktiv"
          PasswordCannotBeEmpty = "Passwort darf nicht leer sein"
          PasswordsDoNotMatch = "Passwörter stimmen nicht überein" }
      Reading =
        { LandingTitle = "BpMonitor"
          LandingTagline = "Erfasse und überprüfe deine Blutdruckwerte."
          HistoryTitle = "Verlauf"
          BloodPressureGraph = "Blutdruck-Diagramm"
          RecentTitle = "Aktuell"
          LoadFullHistory = "Gesamten Verlauf laden"
          ChartCitationPrefix = "Diagramm-Layout inspiriert von "
          Last7Days = "Letzte 7 Tage"
          Last30Days = "Letzte 30 Tage"
          RecentReadingsSection = "Angezeigte Messwerte"
          AddReadingTitle = "Messung hinzufügen"
          EditReadingTitle = "Messung bearbeiten"
          TimestampHint = "yyyy-MM-dd HH:mm" }
      Member =
        { FamilyMembersTitle = "Familienmitglieder"
          AddFamilyMember = "Familienmitglied hinzufügen"
          AddMember = "Mitglied hinzufügen"
          EditMemberTitle = "Mitglied bearbeiten"
          AdminHeader = "Admin"
          ActiveHeader = "Aktiv"
          PasswordHeader = "Passwort"
          AdminBadge = "Admin"
          ActiveBadge = "Aktiv"
          ClaimedBadge = "Aktiviert"
          UnclaimedBadge = "Nicht aktiviert"
          NoneBadge = "—"
          You = "Du"
          ResetPassword = "Passwort zurücksetzen"
          AdminCheckboxLabel = " Admin"
          ActiveCheckboxLabel = " Aktiv"
          LanguageTitle = "Sprache"
          GoalRangeTitle = "Zielbereich"
          SystolicMin = "Systolisch min"
          SystolicMax = "Systolisch max"
          DiastolicMin = "Diastolisch min"
          DiastolicMax = "Diastolisch max" }
      Medication =
        { Required = "Erforderlich"
          Optional = "Optional"
          DeleteConfirm = fun name -> $"{name} löschen? Dies kann nicht rückgängig gemacht werden."
          MedicationsTitle = "Medikamente"
          FullNameHeader = "Vollständiger Name"
          StartHeader = "Start"
          EndHeader = "Ende"
          AddMedicationTitle = "Medikament hinzufügen"
          NameHint = "Kurzbezeichnung, die im Zeitstrahl angezeigt wird, z. B. HCTZ"
          FullNameHint = "Ausführliche Form, angezeigt im Tooltip des Zeitstrahls"
          StartDateLabel = "Startdatum"
          StartDateHint = "dd.mm.yyyy"
          EndDateLabel = "Enddatum"
          EndDateHint = "dd.mm.yyyy"
          EditMedicationTitle = "Medikament bearbeiten"
          MedicationsTimelineTitle = "Medikamenten-Zeitstrahl"
          NameIsEmpty = "Name darf nicht leer sein"
          EndDateBeforeStartDate = "Enddatum muss am oder nach dem Startdatum liegen" }
      Trend =
        { TrendsTitle = "Trends"
          Weekly = "Wöchentlich"
          Monthly = "Monatlich"
          Yearly = "Jährlich"
          NoReadingsIn = fun label -> $"Keine Messungen in {label}."
          Readings = "Messungen"
          AvgSystolic = "Ø Systolisch"
          AvgDiastolic = "Ø Diastolisch"
          AvgHeartRate = "Ø Herzfrequenz"
          StatValue = fun avg mn mx -> $"{avg} (Min: {mn}, Max: {mx})"
          ThisWeek = "Diese Woche"
          LastWeek = "Letzte Woche"
          CalendarWeek = fun week -> $"KW {week}"
          CalendarWeekOfYear = fun week year -> $"KW {week}/{year}"
          ThisMonth = "Dieser Monat"
          LastMonth = "Letzter Monat"
          MonthOfYear =
            fun month year ->
              System.DateTime(year, month, 1).ToString("MMM yyyy", System.Globalization.CultureInfo("de-DE"))
          ThisYear = "Dieses Jahr"
          LastYear = "Letztes Jahr"
          Year = fun year -> string year }
      Errors =
        { NotAnInteger = fun label value -> $"{label}: '{value}' ist keine gültige Ganzzahl"
          NotAValidDateTime = fun value -> $"Zeitstempel: '{value}' ist kein gültiges Datum/keine gültige Uhrzeit"
          NotAValidDate = fun label value -> $"{label}: '{value}' ist kein gültiges Datum (erwartet: dd.mm.yyyy)"
          NameIsEmpty = "Name darf nicht leer sein"
          AtLeastOneActiveAdmin = "Mindestens ein Mitglied muss ein aktiver Admin sein"
          SystolicMinMustBeLessThanMax = "Systolisch min muss kleiner sein als Systolisch max"
          DiastolicMinMustBeLessThanMax = "Diastolisch min muss kleiner sein als Diastolisch max"
          SystolicOutOfRange = fun v lo hi -> $"Systolisch {v} liegt außerhalb des Bereichs ({lo}–{hi})"
          DiastolicOutOfRange = fun v lo hi -> $"Diastolisch {v} liegt außerhalb des Bereichs ({lo}–{hi})"
          HeartRateOutOfRange = fun v lo hi -> $"Herzfrequenz {v} liegt außerhalb des Bereichs ({lo}–{hi})" }
      Charts =
        { Systolic = "Systolisch"
          Diastolic = "Diastolisch"
          SystolicTrend = "Systolisch (Trend)"
          DiastolicTrend = "Diastolisch (Trend)"
          Comments = "Kommentare"
          Ongoing = "laufend"
          AxisTitle = "Blutdruck [mmHg]"
          CalendarWeekTick = fun week -> $"KW{week}"
          DayMonthTick = fun date -> date.ToString("d. MMM", System.Globalization.CultureInfo("de-DE"))
          MonthTick = fun date -> date.ToString("MMM", System.Globalization.CultureInfo("de-DE"))
          ShortWeekdays =
            System.Globalization.CultureInfo("de-DE").DateTimeFormat.AbbreviatedDayNames
            |> Array.toList } }

  /// Every language routes through here, so a third language is one new value
  /// plus one match arm.
  let forLanguage =
    function
    | English -> en
    | German -> de
