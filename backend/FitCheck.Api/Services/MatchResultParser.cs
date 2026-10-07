using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FitCheck.Api.Infrastructure;
using FitCheck.Api.Models;

namespace FitCheck.Api.Services;

/// <summary>
/// Validates the model's JSON and checks it against the CV text. The model is trusted to judge fit, but not to
/// claim a keyword is missing when it's written in the CV verbatim: those are moved to matched.
/// </summary>
public static class MatchResultParser
{
    public const int MaxMatched = 25;
    public const int MaxMissing = 20;
    public const int TipCount = 3;
    private const int MaxKeywordLength = 60;

    public static MatchResult Parse(string raw, string cvText)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(LlmText.StripCodeFences(raw)) as JsonObject
                ?? throw new LlmException("The model's response wasn't a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new LlmException("The model returned malformed JSON.", ex);
        }

        var score = GetNumber(root["matchScore"])
            ?? throw new LlmException("The model's response had no match score.");

        var matched = DistinctKeywords((root["matchedSkills"] as JsonArray ?? []).Select(GetString)).ToList();

        var missing = new List<MissingKeyword>();
        foreach (var item in root["missingKeywords"] as JsonArray ?? [])
        {
            var (keyword, importance) = item switch
            {
                JsonObject o => (CleanKeyword(GetString(o["keyword"])), ParseImportance(GetString(o["importance"]))),
                _ => (CleanKeyword(GetString(item)), Importance.Medium),
            };
            if (keyword is null || missing.Any(m => Same(m.Keyword, keyword)) || matched.Any(m => Same(m, keyword)))
                continue;

            if (ContainsTerm(cvText, keyword))
                matched.Add(keyword); // The CV does say it; the model missed it.
            else
                missing.Add(new MissingKeyword(keyword, importance));
        }

        var tips = (root["tips"] as JsonArray ?? [])
            .Select(ParseTip)
            .OfType<ImprovementTip>()
            .Take(TipCount)
            .ToList();
        if (tips.Count == 0)
            throw new LlmException("The model didn't return any improvement tips.");

        return new MatchResult(
            JobTitle: NullIfBlank(GetString(root["jobTitle"])),
            Company: NullIfBlank(GetString(root["company"])),
            MatchScore: (int)Math.Round(Math.Clamp(score, 0, 100)),
            Summary: GetString(root["summary"])?.Trim() ?? "",
            Details: new MatchDetails(
                matched.Take(MaxMatched).ToList(),
                missing.OrderBy(m => m.Importance).Take(MaxMissing).ToList(),
                tips));
    }

    /// <summary>Case-insensitive whole-term match, tolerant of extra whitespace ("C#", "Node.js" and "CI/CD" work).</summary>
    public static bool ContainsTerm(string text, string term)
    {
        term = term.Trim();
        if (term.Length == 0)
            return false;

        var pattern = string.Join(@"\s+", term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));
        // Word boundaries only where the term itself starts/ends with a letter or digit, so "Java" doesn't match
        // "JavaScript" but ".NET" still matches inside "ASP.NET".
        var before = char.IsLetterOrDigit(term[0]) ? @"(?<![\p{L}\p{N}])" : "";
        var after = char.IsLetterOrDigit(term[^1]) ? @"(?![\p{L}\p{N}])" : "";
        return Regex.IsMatch(text, before + pattern + after,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    }

    private static IEnumerable<string> DistinctKeywords(IEnumerable<string?> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var keyword = CleanKeyword(value);
            if (keyword is not null && seen.Add(keyword))
                yield return keyword;
        }
    }

    private static ImprovementTip? ParseTip(JsonNode? node)
    {
        var (title, detail) = node switch
        {
            JsonObject o => (GetString(o["title"])?.Trim(), GetString(o["detail"])?.Trim()),
            _ => (null, GetString(node)?.Trim()),
        };
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(detail))
            return null;
        return new ImprovementTip(string.IsNullOrEmpty(title) ? "Tip" : title, detail ?? "");
    }

    private static Importance ParseImportance(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "high" => Importance.High,
        "low" => Importance.Low,
        _ => Importance.Medium,
    };

    private static string? CleanKeyword(string? value)
    {
        var keyword = value?.Trim().Trim('"', '\'', '.', ',', ';');
        return string.IsNullOrEmpty(keyword) || keyword.Length > MaxKeywordLength ? null : keyword;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    private static double? GetNumber(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<double>(out var d))
            return d;
        if (value.TryGetValue<string>(out var s) &&
            double.TryParse(s.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            return d;
        return null;
    }
}
