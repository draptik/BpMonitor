namespace BpMonitor.Core

open System
open System.Security.Cryptography

/// Pure password hashing using PBKDF2-SHA256 (BCL only, no external packages).
/// Encoded format: "<iterations>.<base64-salt>.<base64-hash>"
module PasswordHashing =
  [<Literal>]
  let private Iterations = 310_000

  [<Literal>]
  let private SaltBytes = 32

  [<Literal>]
  let private HashBytes = 32

  /// Hashes a plaintext password and returns a self-contained encoded string
  /// that includes the iteration count, salt, and derived key.
  let hash (password: string) : string =
    let salt = RandomNumberGenerator.GetBytes(SaltBytes)

    let derived =
      Rfc2898DeriveBytes.Pbkdf2(
        System.Text.Encoding.UTF8.GetBytes(password),
        salt,
        Iterations,
        HashAlgorithmName.SHA256,
        HashBytes
      )

    $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(derived)}"

  /// Verifies a plaintext password against an encoded hash produced by `hash`.
  /// Returns false (not an exception) on any malformed input.
  let verify (password: string) (encoded: string) : bool =
    let parts = encoded.Split('.')

    if parts.Length <> 3 then
      false
    else
      match Int32.TryParse(parts[0]) with
      | false, _ -> false
      | true, iterations ->
        try
          let salt = Convert.FromBase64String(parts[1])
          let expected = Convert.FromBase64String(parts[2])

          let actual =
            Rfc2898DeriveBytes.Pbkdf2(
              System.Text.Encoding.UTF8.GetBytes(password),
              salt,
              iterations,
              HashAlgorithmName.SHA256,
              expected.Length
            )

          CryptographicOperations.FixedTimeEquals(ReadOnlySpan(actual), ReadOnlySpan(expected))
        with _ ->
          false
