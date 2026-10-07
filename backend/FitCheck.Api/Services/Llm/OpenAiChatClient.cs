using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FitCheck.Api.Infrastructure;
using FitCheck.Api.Options;
using Microsoft.Extensions.Options;

namespace FitCheck.Api.Services.Llm;

/// <summary>Calls the OpenAI Chat Completions API over plain HTTP.</summary>
public sealed class OpenAiChatClient(HttpClient http, IOptions<OpenAiOptions> options, ILogger<OpenAiChatClient> logger)
    : ILlmClient
{
    public string Model => options.Value.Model;

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, bool jsonMode, CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey))
            throw new LlmException("OpenAI API key is not configured. Set OpenAI:ApiKey in appsettings.Development.json.");

        var payload = new JsonObject
        {
            ["model"] = o.Model,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt }),
            ["max_completion_tokens"] = o.MaxCompletionTokens,
        };
        if (jsonMode)
            payload["response_format"] = new JsonObject { ["type"] = "json_object" };

        var url = $"{o.BaseUrl.TrimEnd('/')}/chat/completions";
        var json = payload.ToJsonString();

        var response = await LlmHttp.SendWithRetryAsync(http, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
            return request;
        }, "OpenAI", logger, cancellationToken);

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = TryGetErrorMessage(body) ?? response.ReasonPhrase ?? "unknown error";
                logger.LogWarning("OpenAI returned {Status}: {Message}", (int)response.StatusCode, message);
                throw new LlmException($"OpenAI returned {(int)response.StatusCode}: {message}");
            }

            JsonNode? choice;
            try
            {
                choice = JsonNode.Parse(body)?["choices"]?[0];
            }
            catch (JsonException ex)
            {
                throw new LlmException("OpenAI returned an unreadable response.", ex);
            }

            if (choice?["finish_reason"]?.GetValue<string>() == "length")
                throw new LlmException(
                    "The model's response was cut off. Try a smaller spec or raise OpenAI:MaxCompletionTokens.");

            var content = choice?["message"]?["content"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(content))
                throw new LlmException("The model returned an empty response.");

            return content;
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
