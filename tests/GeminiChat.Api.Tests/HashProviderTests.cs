using GeminiChat.Api.Features.Auth;

namespace GeminiChat.Api.Tests;

public class HashProviderTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        // Arrange: hash a known test password, the way a stored password hash is created
        const string password = "test-password";
        string storedHash = HashProvider.Hash(password);

        // Act: check the same password against the stored hash, the way login does
        bool isValid = HashProvider.Verify(password, storedHash);

        // Assert: the correct password must be accepted
        Assert.True(isValid);
    }

    [Fact]
    public void Hash_Returns160HexCharacters()
    {
        // Arrange: any test password works, the length doesn't depend on it
        const string password = "test-password";

        // Act
        string storedHash = HashProvider.Hash(password);

        // Assert: 16-byte salt + 64-byte hash = 80 bytes, and each byte becomes 2 hex characters
        Assert.Equal(160, storedHash.Length);
    }

    [Theory]
    [InlineData("")]                        // empty password
    [InlineData("pässwörd-ćčšđž-密码")]       // non-English characters
    [InlineData("a-very-long-test-password-that-goes-on-and-on-and-on-and-on-and-on-and-on")]
    public void Verify_ReturnsTrue_ForCorrectPassword_OfAnyKind(string password)
    {
        // Arrange: hash the password given by [InlineData]
        string storedHash = HashProvider.Hash(password);

        // Act
        bool isValid = HashProvider.Verify(password, storedHash);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Verify_Throws_WhenStoredHashIsTooShort()
    {
        // Arrange: a "stored hash" shorter than the 32-character salt Verify expects
        const string password = "test-password";
        const string brokenHash = "too-short";

        // Act + Assert: Verify must throw; the lambda () => ... is called inside Assert.Throws
        Assert.Throws<ArgumentOutOfRangeException>(() => HashProvider.Verify(password, brokenHash));
    }
}
