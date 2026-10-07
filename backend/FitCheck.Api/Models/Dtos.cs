using System.ComponentModel.DataAnnotations;

namespace FitCheck.Api.Models;

public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = "";

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(128, ErrorMessage = "Password must be at most 128 characters.")]
    public string Password { get; init; } = "";
}

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";
}

public sealed record AuthResponse(string Token, DateTime ExpiresAt, string Email, bool IsGuest);

public sealed record UserDto(Guid Id, string Email, bool IsGuest);

/// <summary>multipart/form-data body for creating an analysis.</summary>
public sealed class AnalyzeRequest
{
    [Required(ErrorMessage = "Attach your CV as a PDF or DOCX file.")]
    public IFormFile? Cv { get; init; }

    [Required(ErrorMessage = "Paste the job description.")]
    public string JobDescription { get; init; } = "";

    [MaxLength(200)]
    public string? Title { get; init; }
}

public sealed record AnalysisSummaryDto(
    Guid Id,
    string Title,
    string? Company,
    int MatchScore,
    string FileName,
    DateTime CreatedAt);

public sealed record AnalysisDto(
    Guid Id,
    string Title,
    string? Company,
    string FileName,
    int MatchScore,
    string Summary,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<MissingKeyword> MissingKeywords,
    IReadOnlyList<ImprovementTip> Tips,
    string JobDescription,
    string CvText,
    string Model,
    int DurationMs,
    DateTime CreatedAt);
