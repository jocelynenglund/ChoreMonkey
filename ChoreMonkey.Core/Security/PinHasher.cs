using System.Security.Cryptography;

namespace ChoreMonkey.Core.Security;

public static class PinHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;

    public static string HashPin(int pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            pin.ToString(),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        var result = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, result, SaltSize, HashSize);

        return Convert.ToBase64String(result);
    }

    public static bool VerifyPin(int pin, string hashedPin)
    {
        try
        {
            var hashBytes = Convert.FromBase64String(hashedPin);
            if (hashBytes.Length != SaltSize + HashSize)
                return false;

            var salt = new byte[SaltSize];
            Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);

            var storedHash = new byte[HashSize];
            Buffer.BlockCopy(hashBytes, SaltSize, storedHash, 0, HashSize);

            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                pin.ToString(),
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }
}
