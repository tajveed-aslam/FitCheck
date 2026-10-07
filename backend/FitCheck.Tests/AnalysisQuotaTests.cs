using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FitCheck.Api.Infrastructure;
using FitCheck.Api.Options;
using FitCheck.Api.Services;

namespace FitCheck.Tests;

public class AnalysisQuotaTests
{
    private static ClaimsPrincipal Principal(string id, bool guest)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, id) };
        if (guest)
            claims.Add(new Claim(AppClaims.Guest, "true"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static AnalysisQuota Quota() => new(Microsoft.Extensions.Options.Options.Create(
        new DemoOptions { GuestAnalysesPerHour = 2, UserAnalysesPerHour = 4 }));

    [Fact]
    public void Guests_get_the_guest_allowance_then_a_429_with_retry_after()
    {
        using var quota = Quota();
        var guest = Principal("g1", guest: true);

        quota.Take(guest);
        quota.Take(guest);
        var ex = Assert.Throws<QuotaExceededException>(() => quota.Take(guest));

        Assert.NotNull(ex.RetryAfter);
        Assert.True(ex.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void Registered_users_get_more_and_users_are_counted_separately()
    {
        using var quota = Quota();
        var alice = Principal("alice", guest: false);
        var bob = Principal("bob", guest: false);

        for (var i = 0; i < 4; i++)
            quota.Take(alice);
        Assert.Throws<QuotaExceededException>(() => quota.Take(alice));

        quota.Take(bob); // Alice's usage doesn't affect Bob.
    }
}
