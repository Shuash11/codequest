// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// Tests for: Services/PasswordHasher.cs — the R-2 #2 password hashing
// (PBKDF2-HMAC-SHA256, 600,000 iterations, 16-byte salt, 32-byte key, stored
// as "pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64>").
// Look at: the cases below — one behavior each:
//   1. the SEEDED hash (database/seed.sql line 45, user 'demo') verifies
//      against 'codequest123' — the plan's special rule: if this test ever
//      FAILS the phase stops and reports (seed/parameter mismatch); the hasher
//      parameters and the seed are BOTH reviewer-verified artifacts and are
//      never tweaked to make a failing test pass,
//   2. wrong passwords are rejected,
//   3. hash-then-verify round-trip (and the salt is random: two hashes of the
//      same password differ),
//   4. malformed stored strings return false WITHOUT throwing (a broken row
//      must not crash the login).
// Pure logic — no scratch files, no database (fresh-runner convention).
// =============================================================================

using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>
/// Phase P7 password-hashing tests: the seeded credential verifies, wrong
/// credentials and malformed stored strings are rejected safely.
/// </summary>
public class PasswordHasherTests
{
    /// <summary>
    /// The stored hash of the seeded 'demo' account, verbatim from
    /// database/seed.sql (line 45) — verifies against 'codequest123'.
    /// </summary>
    private const string SeededDemoHash = "pbkdf2-sha256:600000:iCiatnQMd0AiHO3ww38HRg==:8vrsmmlXkmYgxd+YeaUNe70EGg0dG1QfxTggC/Unduo=";

    // ----------------------------------------------------------------- 1
    [Fact]
    public void SeededDemoHash_VerifiesAgainstSeedPassword()
    {
        Assert.True(PasswordHasher.Verify(SeededDemoHash, "codequest123"));
    }

    // ----------------------------------------------------------------- 2
    [Theory]
    [InlineData("codequest124")]
    [InlineData("Codequest123")]
    [InlineData("codequest123 ")]
    [InlineData(" codequest123")]
    [InlineData("")]
    public void SeededDemoHash_RejectsWrongPasswords(string wrongPassword)
    {
        Assert.False(PasswordHasher.Verify(SeededDemoHash, wrongPassword));
    }

    // ----------------------------------------------------------------- 3
    [Fact]
    public void HashThenVerify_RoundTrip_AcceptsCorrectPasswordOnly()
    {
        string password = "S3cure-passw0rd!";
        string stored = PasswordHasher.Hash(password);

        Assert.True(PasswordHasher.Verify(stored, password));
        Assert.False(PasswordHasher.Verify(stored, "S3cure-passw0rd"));
        Assert.False(PasswordHasher.Verify(stored, "s3cure-passw0rd!"));
    }

    [Fact]
    public void Hash_SaltsRandomly_TwoHashesOfSamePasswordDiffer()
    {
        string first = PasswordHasher.Hash("round-trip-password");
        string second = PasswordHasher.Hash("round-trip-password");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify(first, "round-trip-password"));
        Assert.True(PasswordHasher.Verify(second, "round-trip-password"));
    }

    [Fact]
    public void Hash_UsesTheR2P2FormatAndParameters()
    {
        string stored = PasswordHasher.Hash("format-check");

        string[] parts = stored.Split(':');
        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal("600000", parts[1]); // R-2 #2 iterations
        Assert.Equal(24, parts[2].Length); // 16-byte salt in base64
        Assert.Equal(44, parts[3].Length); // 32-byte key in base64
    }

    // ----------------------------------------------------------------- 4
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("pbkdf2-sha256")]
    [InlineData("pbkdf2-sha256:600000")]
    [InlineData("pbkdf2-sha256:600000:only-two-parts")] // salt without a key part
    [InlineData("md5:600000:iCiatnQMd0AiHO3ww38HRg==:8vrsmmlXkmYgxd+YeaUNe70EGg0dG1QfxTggC/Unduo=")] // wrong prefix
    [InlineData("pbkdf2-sha256:not-a-number:iCiatnQMd0AiHO3ww38HRg==:8vrsmmlXkmYgxd+YeaUNe70EGg0dG1QfxTggC/Unduo=")]
    [InlineData("pbkdf2-sha256:0:iCiatnQMd0AiHO3ww38HRg==:8vrsmmlXkmYgxd+YeaUNe70EGg0dG1QfxTggC/Unduo=")] // zero iterations
    [InlineData("pbkdf2-sha256:99999999999999:iCiatnQMd0AiHO3ww38HRg==:8vrsmmlXkmYgxd+YeaUNe70EGg0dG1QfxTggC/Unduo=")] // overflow iterations
    [InlineData("pbkdf2-sha256:600000:!!!not-base64!!!:8vrsmmlXkmYgxd+YeaUNe70EGg0dG1QfxTggC/Unduo=")]
    [InlineData("pbkdf2-sha256:600000:iCiatnQMd0AiHO3ww38HRg==:!!!not-base64!!!")]
    [InlineData("pbkdf2-sha256:600000:QUJD:8vrsmmlXkmYgxd+YeaUNe70EGg0dG1QfxTggC/Unduo=")] // salt decodes but is 3 bytes, not 16
    [InlineData("pbkdf2-sha256:600000:iCiatnQMd0AiHO3ww38HRg==:dG9vc2hvcnQ=")] // key decodes but is 8 bytes, not 32
    public void Verify_MalformedStoredStrings_ReturnFalseWithoutThrowing(string storedHash)
    {
        Assert.False(PasswordHasher.Verify(storedHash, "codequest123"));
    }
}
