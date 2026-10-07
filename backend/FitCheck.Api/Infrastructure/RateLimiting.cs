using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FitCheck.Api.Options;
using FitCheck.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FitCheck.Api.Infrastructure;

/// <summary>Protects the LLM quota on a public demo: per-user generation limits and per-IP guest sign-ups.</summary>
public static class RateLimiting
{
    public const string AnalysisPolicy = "analysis";
    public const string GuestSessionPolicy = "guest-session";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, DemoOptions demo)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AnalysisPolicy, context =>
            {
                var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? ClientIp(context);
                var isGuest = context.User.HasClaim(AppClaims.Guest, "true");
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"analysis:{userId}",
                    _ => HourlyWindow(isGuest ? demo.GuestAnalysesPerHour : demo.UserAnalysesPerHour));
            });

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

    private static FixedWindowRateLimiterOptions HourlyWindow(int permits) => new()
    {
        PermitLimit = Math.Max(permits, 1),
        Window = TimeSpan.FromHours(1),
        QueueLimit = 0,
    };

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
