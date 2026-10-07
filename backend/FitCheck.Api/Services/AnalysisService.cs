using System.Diagnostics;
using FitCheck.Api.Infrastructure;
using FitCheck.Api.Models;
using FitCheck.Api.Options;
using FitCheck.Api.Services.Llm;
using Microsoft.Extensions.Options;

namespace FitCheck.Api.Services;

public sealed record AnalysisInput(string FileName, byte[] FileContent, string JobDescription, string? Title);

/// <summary>Validated input with the CV text already extracted: everything needed for the LLM call.</summary>
public sealed record PreparedAnalysis(string FileName, string CvText, string JobDescription, string? Title);

public sealed record AnalysisOutcome(
    string Title,
    string? Company,
    string FileName,
    string CvText,
    string JobDescription,
    MatchResult Result,
    string Model,
    int DurationMs);

public interface IAnalysisService
{
    /// <summary>Validates the input and extracts the CV text. Cheap; never calls the LLM.</summary>
    /// <exception cref="InputValidationException">The file or job description can't be used.</exception>
    PreparedAnalysis Prepare(AnalysisInput input);

    Task<AnalysisOutcome> AnalyzeAsync(PreparedAnalysis prepared, CancellationToken cancellationToken);
}

/// <summary>
/// Split into Prepare and Analyze so callers can spend LLM quota only on requests that are actually valid.
/// </summary>
public sealed class AnalysisService(ILlmClient llm, IOptions<AnalysisOptions> options) : IAnalysisService
{
    private const int MaxTitleLength = 200;
    private const int MaxFileNameLength = 255;

    public PreparedAnalysis Prepare(AnalysisInput input)
    {
        var opts = options.Value;
        var jobDescription = ValidateJobDescription(input.JobDescription, opts);

        if (input.FileContent.Length == 0)
            throw new InputValidationException("The uploaded file is empty.");
        if (input.FileContent.Length > opts.MaxFileBytes)
            throw new InputValidationException($"The CV must be smaller than {opts.MaxFileBytes / (1024 * 1024)} MB.");

        var cvText = DocumentTextExtractor.Extract(input.FileName, input.FileContent, opts.MaxPdfPages);
        if (cvText.Length < opts.MinCvChars)
            throw new InputValidationException(
                "Couldn't find enough text in this CV. If it's a scanned image, upload a text-based PDF or DOCX instead.");
        if (cvText.Length > opts.MaxCvChars)
            cvText = cvText[..opts.MaxCvChars];

        return new PreparedAnalysis(
            Truncate(Path.GetFileName(input.FileName), MaxFileNameLength), cvText, jobDescription, input.Title);
    }

    public async Task<AnalysisOutcome> AnalyzeAsync(PreparedAnalysis prepared, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var raw = await llm.CompleteAsync(
            Prompts.MatchSystem, Prompts.MatchUser(prepared.JobDescription, prepared.CvText), jsonMode: true, cancellationToken);
        var result = MatchResultParser.Parse(raw, prepared.CvText);

        return new AnalysisOutcome(
            Title: Truncate(FirstNonBlank(prepared.Title, result.JobTitle, "Untitled role"), MaxTitleLength),
            Company: result.Company is null ? null : Truncate(result.Company, MaxTitleLength),
            FileName: prepared.FileName,
            CvText: prepared.CvText,
            JobDescription: prepared.JobDescription,
            Result: result,
            Model: llm.Model,
            DurationMs: (int)stopwatch.ElapsedMilliseconds);
    }

    private static string ValidateJobDescription(string? value, AnalysisOptions opts)
    {
        var text = value?.Trim() ?? "";
        if (text.Length < opts.MinJobDescriptionChars)
            throw new InputValidationException(
                $"The job description is too short to compare against (at least {opts.MinJobDescriptionChars} characters).");
        if (text.Length > opts.MaxJobDescriptionChars)
            throw new InputValidationException(
                $"The job description is {text.Length:N0} characters; the limit is {opts.MaxJobDescriptionChars:N0}.");
        return text;
    }

    private static string FirstNonBlank(params string?[] values) =>
        values.First(v => !string.IsNullOrWhiteSpace(v))!.Trim();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
