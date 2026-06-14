module BpMonitor.Export.FileHelpers

open System
open System.IO

let tryWriteString (path: string) (content: string) : Result<unit, string> =
  try
    File.WriteAllText(path, content)
    Ok()
  with
  | :? IOException as ex -> Error ex.Message
  | :? UnauthorizedAccessException as ex -> Error ex.Message
