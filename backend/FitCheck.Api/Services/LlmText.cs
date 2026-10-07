namespace FitCheck.Api.Services;

public static class LlmText
{
    /// <summary>Removes a surrounding ```lang ... ``` fence, which models add even when told not to.</summary>
    public static string StripCodeFences(string text)
    {
        var t = text.Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal))
            return t;

        var firstNewline = t.IndexOf('\n');
        if (firstNewline < 0)
            return t.Trim('`').Trim();

        t = t[(firstNewline + 1)..];
        var closingFence = t.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence >= 0)
            t = t[..closingFence];

        return t.Trim();
    }
}
