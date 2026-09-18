using System.Security.Cryptography;

namespace TaskManagement.Web.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}

/// <summary>
/// PBKDF2 (HMAC-SHA256) password hashing with a per-user random salt.
/// Stored format: {iterations}.{base64 salt}.{base64 hash}
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join('.', Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        var parts = storedHash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var key = Convert.FromBase64String(parts[2]);
            var attempt = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, key.Length);
            return CryptographicOperations.FixedTimeEquals(attempt, key);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
