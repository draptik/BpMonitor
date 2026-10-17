module BpMonitor.TestSupport.CultureScope

open System.Globalization
open System.Threading

/// Runs `f` with the current thread's culture set to `name`, restoring it afterwards.
/// th-TH is a useful probe: its default calendar is Thai Buddhist (2026 is 2569).
let withCulture (name: string) (f: unit -> 'a) : 'a =
  let original = Thread.CurrentThread.CurrentCulture
  Thread.CurrentThread.CurrentCulture <- CultureInfo(name)

  try
    f ()
  finally
    Thread.CurrentThread.CurrentCulture <- original
