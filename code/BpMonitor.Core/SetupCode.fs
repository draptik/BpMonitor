namespace BpMonitor.Core

open System
open System.Security.Cryptography

/// One-time codes an admin hands out so a member can claim their account (see ADR 0012).
/// Displayed as "K7QX-M2PA"; stored only as a PasswordHashing hash.
module SetupCode =
  // No look-alikes (0/O, 1/I/L) and no U, so codes survive being read aloud or copied by hand.
  let private alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789".ToCharArray()

  let private normalize (code: string) =
    code.ToUpperInvariant().Replace("-", "").Replace(" ", "")

  let generate () : string =
    let chars = RandomNumberGenerator.GetItems<char>(ReadOnlySpan alphabet, 8)
    $"{String(chars, 0, 4)}-{String(chars, 4, 4)}"

  let hash (code: string) : string = PasswordHashing.hash (normalize code)

  /// Ignores case, dashes and spaces in `code`.
  let verify (code: string) (encoded: string) : bool =
    PasswordHashing.verify (normalize code) encoded
