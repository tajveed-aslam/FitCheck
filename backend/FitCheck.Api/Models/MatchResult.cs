namespace FitCheck.Api.Models;

public enum Importance
{
    High,
    Medium,
    Low,
}

public sealed record MissingKeyword(string Keyword, Importance Importance);

public sealed record ImprovementTip(string Title, string Detail);

/// <summary>The list parts of an analysis, stored together as one jsonb column.</summary>
public sealed record MatchDetails(
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<MissingKeyword> MissingKeywords,
    IReadOnlyList<ImprovementTip> Tips);

/// <summary>A validated model answer.</summary>
public sealed record MatchResult(
    string? JobTitle,
    string? Company,
    int MatchScore,
    string Summary,
    MatchDetails Details);
