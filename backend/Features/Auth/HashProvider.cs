using System.Security.Cryptography;

namespace GeminiChat.Api.Features.Auth;


public static class HashProvider
{
    //one of the options that are recommended is to use ASP net core built in hasher:
    // - PasswordHasher<T> which exposes these two methods HashPassword(user, password), VerifyHashedPassword(user, storedHash, password)
    // It uses PBKDF2 under the hood,It handles salt, iterations, format, and constant-time comparison 
    // other option that is recommended is to use a third party library like BCrypt.Net-Next
    // wrapping the bcrypt algorithm (a different slow-hash than PBKDF2, designed from the start for passwords
    // BCrypt.HashPassword(password) and BCrypt.Verify(password, hash) — even simpler. 
    // Bcrypt embeds the salt and cost factor inside the output string automatically (the PHC-style format), 
    // so there's zero separator/splitting code. Widely respected for password hashing specifically.
    private const int SaltSize = 16;
    private const int HashSize = 64;
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    // bytes are for computing, strings are for storing/transmitting, reason why we dont pass bytes
    public static string Hash(string password)
    {
        // With salt: each password gets a random salt mixed in before hashing.
        // Now Hash("password123" + salt_A) and Hash("password123" + salt_B) produce completely different output
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        // Hash() now stores salt FIRST, then hash, with no separator:
        return Convert.ToHexString(salt) + Convert.ToHexString(hash);
    }

    public static bool Verify(string password, string passwordHash)
    {
        // Verify() splits by computed position:
        int saltCharLength = SaltSize * 2;   // 16 bytes → 32 hex chars

        string saltHex = passwordHash.Substring(0, saltCharLength);   // first 32 chars = salt
        string hashHex = passwordHash.Substring(saltCharLength);      // rest = hash

        byte[] salt = Convert.FromHexString(saltHex);
        byte[] hash = Convert.FromHexString(hashHex);
        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(hash, inputHash);
    }

}


