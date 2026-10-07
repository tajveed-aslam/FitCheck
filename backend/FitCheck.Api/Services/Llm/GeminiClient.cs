using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FitCheck.Api.Infrastructure;
using FitCheck.Api.Options;
using Microsoft.Extensions.Options;

namespace FitCheck.Api.Services.Llm;

/// <summary>Calls the Gemini generateContent REST API.</summary>
public sealed class GeminiClient(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
    : ILlmClient
{
    public string Model => options.Value.Model;

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, bool jsonMode, CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey))
            throw new LlmException("Gemini API key is not configured. Set Gemini:ApiKey in appsettings.Development.json.");

        var generationConfig = new JsonObject { ["maxOutputTokens"] = o.MaxOutputTokens };
        if (jsonMode)
            generationConfig["responseMimeType"] = "application/json";

        var payload = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = systemPrompt }) },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = userPrompt }),
            }),
            ["generationConfig"] = generationConfig,
        };

        var url = $"{o.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(o.Model)}:generateContent";
        var json = payload.ToJsonString();

        var response = await LlmHttp.SendWithRetryAsync(http, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            // Header rather than ?key= so the key never lands in URL logs.
            request.Headers.Add("x-goog-api-key", o.ApiKey);
            return request;
        }, "Gemini", logger, cancellationToken);

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = TryGetErrorMessage(body) ?? response.ReasonPhrase ?? "unknown error";
                logger.LogWarning("Gemini returned {Status}: {Message}", (int)response.StatusCode, message);
                throw new LlmException($"Gemini returned {(int)response.StatusCode}: {message}");
            }

            JsonNode? candidate;
            try
            {
                var root = JsonNode.Parse(body);
                candidate = root?["candidates"]?[0];
                if (candidate is null)
                {
                    var blockReason = root?["promptFeedback"]?["blockReason"]?.GetValue<string>();
                    throw new LlmException(blockReason is null
                        ? "Gemini returned no candidates."
                        : $"Gemini blocked the request ({blockReason}).");
                }
            }
            catch (JsonException ex)
            {
                throw new LlmException("Gemini returned an unreadable response.", ex);
            }

            var finishReason = candidate["finishReason"]?.GetValue<string>();
            if (finishReason == "MAX_TOKENS")
                throw new LlmException(
                    "The model's response was cut off. Try a smaller spec or raise Gemini:MaxOutputTokens.");

            // Skip "thought" parts; join the answer text.
            var text = string.Concat(
                (candidate["content"]?["parts"] as JsonArray ?? [])
                    .Where(p => p?["thought"]?.GetValue<bool>() != true)
                    .Select(p => p?["text"]?.GetValue<string>()));

            if (string.IsNullOrWhiteSpace(text))
                throw new LlmException(finishReason is null or "STOP"
                    ? "The model returned an empty response."
                    : $"The model stopped without an answer ({finishReason}).");

            return text;
        }
    }

    private static string? TryGetErrorMessage(string body)
    {
        try
        {
            return JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
