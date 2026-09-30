using System.Security.Cryptography;
using System.Text;

namespace IsgCevreYonetim.Infrastructure.Security;

/// <summary>
/// Parolaları PBKDF2-SHA256 ile türetir. Eski SHA-256 kayıtlarını doğrulayarak
/// kullanıcı girişinde kesintisiz şekilde yeni formata yükseltmeye izin verir.
/// </summary>
public static class PasswordSecurity
{
    private const string Scheme = "PBKDF2-SHA256";
    private const int Iterations = 210_000;
    private const int SaltSize = 32;
    private const int KeySize = 32;

    public static string GenerateSalt()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSize));

    public static string HashPassword(string password, string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(salt);

        var saltBytes = Convert.FromBase64String(salt);
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Scheme}${Iterations}${Convert.ToBase64String(derived)}";
    }

    public static bool Verify(string password, string salt, string? storedHash, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(salt) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            if (storedHash.StartsWith(Scheme + "$", StringComparison.Ordinal))
            {
                var parts = storedHash.Split('$', 3);
                if (parts.Length != 3 || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
                    return false;

                var expected = Convert.FromBase64String(parts[2]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    Convert.FromBase64String(salt),
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);

                needsRehash = iterations < Iterations;
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            // Legacy: SHA256(password + salt). Başarılı girişten sonra PBKDF2'ye yükseltilir.
            using var sha256 = SHA256.Create();
            var legacyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + salt));
            var expectedLegacy = Convert.FromBase64String(storedHash);
            var valid = CryptographicOperations.FixedTimeEquals(legacyBytes, expectedLegacy);
            needsRehash = valid;
            return valid;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
