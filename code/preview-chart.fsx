open System

let scriptDir = __SOURCE_DIRECTORY__

#r "nuget: Plotly.NET, 5.1.0"
#r "BpMonitor.Core/bin/Debug/net10.0/BpMonitor.Core.dll"
#r "BpMonitor.Charts/bin/Debug/net10.0/BpMonitor.Charts.dll"

open BpMonitor.Core
open BpMonitor.Charts

let readings = [
    { Id =  1; Systolic = 120; Diastolic = 80; HeartRate = 70; Timestamp = DateTimeOffset(2026, 1,  1,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id =  2; Systolic = 135; Diastolic = 88; HeartRate = 78; Timestamp = DateTimeOffset(2026, 1,  2,  8, 0, 0, TimeSpan.Zero); Comments = Some "After coffee" }
    { Id =  3; Systolic = 118; Diastolic = 76; HeartRate = 65; Timestamp = DateTimeOffset(2026, 1,  3, 10, 0, 0, TimeSpan.Zero); Comments = None }
    { Id =  4; Systolic = 142; Diastolic = 92; HeartRate = 82; Timestamp = DateTimeOffset(2026, 1,  4,  7, 0, 0, TimeSpan.Zero); Comments = Some "Stressful day" }
    { Id =  5; Systolic = 125; Diastolic = 83; HeartRate = 72; Timestamp = DateTimeOffset(2026, 1,  5,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id =  6; Systolic = 130; Diastolic = 85; HeartRate = 74; Timestamp = DateTimeOffset(2026, 1,  6,  8, 0, 0, TimeSpan.Zero); Comments = None }
    { Id =  7; Systolic = 115; Diastolic = 75; HeartRate = 68; Timestamp = DateTimeOffset(2026, 1,  7, 11, 0, 0, TimeSpan.Zero); Comments = Some "After walk" }
    { Id =  8; Systolic = 128; Diastolic = 84; HeartRate = 73; Timestamp = DateTimeOffset(2026, 1,  8,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id =  9; Systolic = 138; Diastolic = 90; HeartRate = 80; Timestamp = DateTimeOffset(2026, 1,  9,  8, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 10; Systolic = 122; Diastolic = 81; HeartRate = 71; Timestamp = DateTimeOffset(2026, 1, 10,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 11; Systolic = 117; Diastolic = 78; HeartRate = 67; Timestamp = DateTimeOffset(2026, 1, 11,  7, 0, 0, TimeSpan.Zero); Comments = Some "Good sleep" }
    { Id = 12; Systolic = 132; Diastolic = 87; HeartRate = 76; Timestamp = DateTimeOffset(2026, 1, 12,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 13; Systolic = 145; Diastolic = 95; HeartRate = 85; Timestamp = DateTimeOffset(2026, 1, 13,  8, 0, 0, TimeSpan.Zero); Comments = Some "No sleep" }
    { Id = 14; Systolic = 119; Diastolic = 79; HeartRate = 69; Timestamp = DateTimeOffset(2026, 1, 14, 10, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 15; Systolic = 126; Diastolic = 82; HeartRate = 72; Timestamp = DateTimeOffset(2026, 1, 15,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 16; Systolic = 133; Diastolic = 88; HeartRate = 77; Timestamp = DateTimeOffset(2026, 1, 16,  8, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 17; Systolic = 121; Diastolic = 80; HeartRate = 70; Timestamp = DateTimeOffset(2026, 1, 17,  9, 0, 0, TimeSpan.Zero); Comments = Some "Relaxed" }
    { Id = 18; Systolic = 129; Diastolic = 85; HeartRate = 74; Timestamp = DateTimeOffset(2026, 1, 18,  7, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 19; Systolic = 140; Diastolic = 91; HeartRate = 81; Timestamp = DateTimeOffset(2026, 1, 19,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 20; Systolic = 116; Diastolic = 76; HeartRate = 66; Timestamp = DateTimeOffset(2026, 1, 20, 10, 0, 0, TimeSpan.Zero); Comments = Some "After gym" }
    { Id = 21; Systolic = 124; Diastolic = 82; HeartRate = 71; Timestamp = DateTimeOffset(2026, 1, 21,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 22; Systolic = 131; Diastolic = 86; HeartRate = 75; Timestamp = DateTimeOffset(2026, 1, 22,  8, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 23; Systolic = 118; Diastolic = 77; HeartRate = 68; Timestamp = DateTimeOffset(2026, 1, 23,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 24; Systolic = 136; Diastolic = 89; HeartRate = 79; Timestamp = DateTimeOffset(2026, 1, 24,  7, 0, 0, TimeSpan.Zero); Comments = Some "Late night" }
    { Id = 25; Systolic = 123; Diastolic = 81; HeartRate = 72; Timestamp = DateTimeOffset(2026, 1, 25,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 26; Systolic = 127; Diastolic = 83; HeartRate = 73; Timestamp = DateTimeOffset(2026, 1, 26, 10, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 27; Systolic = 143; Diastolic = 93; HeartRate = 83; Timestamp = DateTimeOffset(2026, 1, 27,  8, 0, 0, TimeSpan.Zero); Comments = Some "Work deadline" }
    { Id = 28; Systolic = 119; Diastolic = 78; HeartRate = 69; Timestamp = DateTimeOffset(2026, 1, 28,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 29; Systolic = 122; Diastolic = 80; HeartRate = 71; Timestamp = DateTimeOffset(2026, 1, 29,  9, 0, 0, TimeSpan.Zero); Comments = None }
    { Id = 30; Systolic = 128; Diastolic = 84; HeartRate = 74; Timestamp = DateTimeOffset(2026, 1, 30,  8, 0, 0, TimeSpan.Zero); Comments = None }
]

let out = if fsi.CommandLineArgs.Length > 1 then fsi.CommandLineArgs[1] else "/tmp/bpchart-preview.html"
IO.File.WriteAllText(out, BpChart.toHtml readings)
printfn "Written to %s" out
