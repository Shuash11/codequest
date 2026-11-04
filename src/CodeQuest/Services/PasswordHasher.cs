// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation (login)
// R-2 #2 obligation: the password hashing of the accounts (schema column
// users.password_hash). Additive service only — the plan freezes the existing
// services; this file is the one service P7 is allowed to add.
//
// PARAMETERS (binding — plan ruling R-2 #2, from the OWASP Password Storage
// Cheat Sheet recommendation of PBKDF2 at 600,000 iterations; stated here for
// the instructor walkthrough and for the P15 security review):
//
//   algorithm  : PBKDF2-HMAC-SHA256 — built-in Rfc2898DeriveBytes
//                (HashAlgorithmName.SHA256), no new package
//   iterations : 600,000
//   salt       : random 16 bytes, generated fresh for every hash
//   derived key: 32 bytes
//   encoding   : UTF-8 password bytes
//   storage    : "pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64>"
//                (standard base64 alphabet) — the exact format the seeded
//                demo row carries (database/seed.sql, user 'demo')
//
// Look at: Hash() — generates the random salt, derives the key, formats the
//          stored string. Verify() — parses the stored string, re-derives the
//          key with the STORED parameters, compares in constant time
//          (CryptographicOperations.FixedTimeEquals); every malformed input
//          parses to "false", never an exception. The plaintext password is
//          never stored, logged or returned.
// =============================================================================

using System.Security.Cryptography;
using System.Text;

namespace CodeQuest.Services;

/// <summary>
/// Hashes and verifies passwords in the storage format of plan ruling R-2 #2
/// (PBKDF2-HMAC-SHA256, 600,000 iterations, 16-byte salt, 32-byte key).
/// </summary>
public static class PasswordHasher
{
    /// <summary>The PBKDF2 iteration count of a freshly hashed password (R-2 #2).</summary>
    public const int Iterations = 600_000;

    /// <summary>The random salt size in bytes of a freshly hashed password (R-2 #2).</summary>
    public const int SaltSizeBytes = 16;

    /// <summary>The derived key size in bytes (R-2 #2).</summary>
    public const int HashSizeBytes = 32;

    /// <summary>The prefix of the stored format "pbkdf2-sha256:...".</summary>
    private const string FormatPrefix = "pbkdf2-sha256";

    /// <summary>
    /// Sanity bound for the iteration count read back from a stored string.
    /// Verify() honors the parameters a stored string carries; the bound only
    /// rejects absurd values so a corrupted row cannot hang the login. The
    /// seeded value (600,000) and every value this class hashes are far below.
    /// </summary>
    private const int MaxIterations = 10_000_000;

    /// <summary>
    /// Hashes a password into the storage format
    /// "pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64>" with a fresh
    /// random 16-byte salt (R-2 #2).
    /// </summary>
    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] derivedKey = DeriveKey(password, salt, Iterations);

        return $"{FormatPrefix}:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(derivedKey)}";
    }

    /// <summary>
    /// Verifies a password against a stored hash string. Returns false for a
    /// wrong password AND for every malformed stored string — parsing never
    /// throws (a broken row must not crash the login).
    /// </summary>
    public static bool Verify(string storedHash, string password)
    {
        ArgumentNullException.ThrowIfNull(storedHash);
        ArgumentNullException.ThrowIfNull(password);

        if (!TryParseStoredHash(storedHash, out int iterations, out byte[] salt, out byte[] expectedKey))
        {
            return false;
        }

        byte[] actualKey = DeriveKey(password, salt, iterations);

        // Constant-time comparison: the verdict must not leak how many bytes
        // matched (the "constant-time-ish" verify of the delegation —
        // FixedTimeEquals is the platform primitive for exactly this).
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }

    /// <summary>
    /// Derives the key of one password with the given parameters — the single
    /// place that fixes algorithm, encoding and key size (see the header).
    /// Rfc2898DeriveBytes.Pbkdf2 is the class's current entry point (the
    /// instance constructors are marked obsolete); it is the same built-in
    /// PBKDF2-HMAC-SHA256 implementation the R-2 #2 ruling names.
    /// </summary>
    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSizeBytes);
    }

    /// <summary>
    /// Parses one stored hash string of the format
    /// "pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64>". Returns false —
    /// with empty out values — for anything malformed: wrong prefix, wrong
    /// part count, a non-numeric or out-of-range iteration count, base64 that
    /// does not decode, or a salt/key of the wrong size (R-2 #2 pins 16 and
    /// 32 bytes).
    /// </summary>
    private static bool TryParseStoredHash(string storedHash, out int iterations, out byte[] salt, out byte[] derivedKey)
    {
        iterations = 0;
        salt = Array.Empty<byte>();
        derivedKey = Array.Empty<byte>();

        string[] parts = storedHash.Split(':');
        if (parts.Length != 4 || parts[0] != FormatPrefix)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out int parsedIterations)
            || parsedIterations <= 0
            || parsedIterations > MaxIterations)
        {
            return false;
        }

        byte[] parsedSalt;
        byte[] parsedKey;
        try
        {
            parsedSalt = Convert.FromBase64String(parts[2]);
            parsedKey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (parsedSalt.Length != SaltSizeBytes || parsedKey.Length != HashSizeBytes)
        {
            return false;
        }

        iterations = parsedIterations;
        salt = parsedSalt;
        derivedKey = parsedKey;
        return true;
    }
}
