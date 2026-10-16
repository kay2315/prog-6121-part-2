using Microsoft.AspNetCore.Identity;
using RaceDay.Api;
using Xunit;

namespace RaceDay.Tests;

public class AuthSecurityTests
{
    [Fact]
    public void PasswordHasher_DoesNotStorePlainText_AndVerifiesCorrectPassword()
    {
        var user = new AppUser { Email = "runner@example.com", Role = "Participant" };
        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, "CorrectHorseBatteryStaple!42");
        Assert.NotEqual("CorrectHorseBatteryStaple!42", user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, "CorrectHorseBatteryStaple!42"));
    }

    [Fact]
    public void PasswordHasher_RejectsIncorrectPassword()
    {
        var user = new AppUser { Email = "runner@example.com", Role = "Participant" };
        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, "CorrectHorseBatteryStaple!42");
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash, "wrong-password"));
    }

    [Theory]
    [InlineData("Organiser", true)]
    [InlineData("Participant", true)]
    [InlineData("Admin", false)]
    [InlineData("", false)]
    public void OnlySupportedRolesAreValid(string role, bool expected)
        => Assert.Equal(expected, role is "Organiser" or "Participant");
}
