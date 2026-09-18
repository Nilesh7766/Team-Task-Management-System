using TaskManagement.Web.Services;
using Xunit;

namespace TaskManagement.Tests;

public class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();

    [Fact]
    public void Hash_does_not_store_the_plain_password()
    {
        var hash = _hasher.Hash("Secret@123");

        Assert.DoesNotContain("Secret@123", hash);
        Assert.Equal(3, hash.Split('.').Length);
    }

    [Fact]
    public void Verify_accepts_the_correct_password()
        => Assert.True(_hasher.Verify("Secret@123", _hasher.Hash("Secret@123")));

    [Fact]
    public void Verify_rejects_a_wrong_password()
        => Assert.False(_hasher.Verify("wrong", _hasher.Hash("Secret@123")));

    [Fact]
    public void Two_hashes_of_the_same_password_differ_because_of_the_salt()
    {
        var first = _hasher.Hash("Secret@123");
        var second = _hasher.Hash("Secret@123");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("Secret@123", first));
        Assert.True(_hasher.Verify("Secret@123", second));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-hash")]
    [InlineData("1.2.3")]
    public void Verify_rejects_malformed_stored_hashes(string stored)
        => Assert.False(_hasher.Verify("Secret@123", stored));
}
