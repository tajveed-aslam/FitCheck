using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FitCheck.Api.Options;
using FitCheck.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FitCheck.Api.Infrastructure;

/// <summary>
/// Protects a public demo: a coarse per-user cap on upload requests and per-IP guest sign-ups (middleware
/// policies), plus <see cref="AnalysisQuota"/> for the LLM itself.
/// </summary>
public static class RateLimiting
{
    public const string UploadPolicy = "upload";
    public const string GuestSessionPolicy = "guest-session";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, DemoOptions demo)
    {
        services.AddSingleton<AnalysisQuota>();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(UploadPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"upload:{context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? ClientIp(context)}",
                    _ => HourlyWindow(demo.UploadsPerHour)));

            options.AddPolicy(GuestSessionPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"guest:{ClientIp(context)}",
                    _ => HourlyWindow(demo.GuestSessionsPerHourPerIp)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                await response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "You've hit the hourly limit for this demo. Please try again later.",
                }, cancellationToken);
            };
        });

        return services;
    }

    internal static FixedWindowRateLimiterOptions HourlyWindow(int permits) => new()
    {
        PermitLimit = Math.Max(permits, 1),
        Window = TimeSpan.FromHours(1),
        QueueLimit = 0,
    };

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

/// <summary>
/// Hourly LLM quota per user (guests stricter). Taken only once a request has passed validation, so a visitor
/// who picks the wrong file a few times isn't locked out without ever reaching the model.
/// </summary>
public sealed class AnalysisQuota(IOptions<DemoOptions> options) : IDisposable
{
    private readonly PartitionedRateLimiter<ClaimsPrincipal> _limiter =
        PartitionedRateLimiter.Create<ClaimsPrincipal, string>(user =>
        {
            var isGuest = user.HasClaim(AppClaims.Guest, "true");
            var demo = options.Value;
            return RateLimitPartition.GetFixedWindowLimiter(
                user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous",
                _ => RateLimiting.HourlyWindow(isGuest ? demo.GuestAnalysesPerHour : demo.UserAnalysesPerHour));
        });

    /// <exception cref="QuotaExceededException">The user has used up this hour's analyses.</exception>
    public void Take(ClaimsPrincipal user)
    {
        using var lease = _limiter.AttemptAcquire(user);
        if (lease.IsAcquired)
            return;

        lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter);
        throw new QuotaExceededException(
            "You've used all of this hour's analyses for the demo. Please try again later.", retryAfter);
    }

    public void Dispose() => _limiter.Dispose();
}
