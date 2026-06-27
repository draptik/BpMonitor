namespace BpMonitor.Web

/// Single source of truth for HTML form field names — keeps name attributes in
/// views, form-key lookups in handlers, and test form data aligned so a rename
/// is a compile error rather than a silent mismatch.
module FormFields =
  // Reading form
  let systolic = "Systolic"
  let diastolic = "Diastolic"
  let heartRate = "HeartRate"
  let timestamp = "Timestamp"
  let comments = "Comments"
  // Member form
  let name = "Name"
  let isAdmin = "IsAdmin"
  let isActive = "IsActive"
  // Login / password form
  let username = "Username"
  let password = "Password"
  let passwordConfirm = "PasswordConfirm"
  // Settings / goal-range form
  let systolicGoalMin = "SystolicGoalMin"
  let systolicGoalMax = "SystolicGoalMax"
  let diastolicGoalMin = "DiastolicGoalMin"
  let diastolicGoalMax = "DiastolicGoalMax"
