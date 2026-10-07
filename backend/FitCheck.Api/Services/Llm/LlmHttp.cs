using System.Net;
using FitCheck.Api.Infrastructure;

namespace FitCheck.Api.Services.Llm;

/// <summary>Shared HTTP plumbing for the LLM clients: sending with retries on transient provider errors.</summary>
internal static class LlmHttp
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15)];

    private static readonly HashSet<HttpStatusCode> TransientStatuses =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    /// <summary>
    /// Sends the request built by <paramref name="createRequest"/> (a fresh message per attempt), retrying
    /// transient failures. Returns the last response, which the caller owns and must dispose.
    /// </summary>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient http,
        Func<HttpRequestMessage> createRequest,
        string providerName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            using (var request = createRequest())
            {
                try
                {
                    response = await http.SendAsync(request, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    throw new LlmException($"Could not reach the {providerName} API.", ex);
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new LlmException($"The {providerName} API timed out. Try a smaller spec.", ex);
                }
            }

            if (!TransientStatuses.Contains(response.StatusCode) || attempt >= RetryDelays.Length)
                return response;

            logger.LogInformation(
                "{Provider} returned {Status}; retrying in {Delay}s (attempt {Attempt})",
                providerName, (int)response.StatusCode, RetryDelays[attempt].TotalSeconds, attempt + 1);
            response.Dispose();
            await Task.Delay(RetryDelays[attempt], cancellationToken);
        }
    }
}
