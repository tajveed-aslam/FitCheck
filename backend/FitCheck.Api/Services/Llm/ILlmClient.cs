namespace FitCheck.Api.Services.Llm;

public interface ILlmClient
{
    /// <summary>Model identifier recorded with each generation.</summary>
    string Model { get; }

    /// <summary>Runs one chat completion and returns the assistant's text.</summary>
    /// <param name="jsonMode">Ask the model to return a single JSON object.</param>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, bool jsonMode, CancellationToken cancellationToken);
}
