using System.Security.Cryptography;

namespace GestionPharmacie.DAL.Security;

/// <summary>
/// Service cryptographique sécurisé pour le hachage et la vérification des mots de passe.
/// Utilise PBKDF2 avec HMAC-SHA256, sel aléatoire de 128 bits et 100 000 itérations.
/// Format stocké : {iterations}.{salt_base64}.{hash_base64}
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int DefaultIterations = 100_000;

    /// <summary>
    /// Hache un mot de passe en clair avec un sel cryptographique aléatoire.
    /// </summary>
    public static string HashPassword(string plainPassword, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(plainPassword))
        {
            throw new ArgumentException("Le mot de passe ne peut pas être vide.", nameof(plainPassword));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            plainPassword,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize
        );

        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Vérifie si le mot de passe fourni correspond au hash stocké (comparaison à temps constant).
    /// </summary>
    public static bool VerifyPassword(string plainPassword, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string[] parts = storedHash.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out int iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            plainPassword,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length
        );

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
