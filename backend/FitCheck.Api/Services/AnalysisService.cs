using System.Diagnostics;
using FitCheck.Api.Infrastructure;
using FitCheck.Api.Models;
using FitCheck.Api.Options;
using FitCheck.Api.Services.Llm;
using Microsoft.Extensions.Options;

namespace FitCheck.Api.Services;

public sealed record AnalysisInput(string FileName, byte[] FileContent, string JobDescription, string? Title);

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
    Task<AnalysisOutcome> AnalyzeAsync(AnalysisInput input, CancellationToken cancellationToken);
}

public sealed class AnalysisService(ILlmClient llm, IOptions<AnalysisOptions> options) : IAnalysisService
{
    private const int MaxTitleLength = 200;
    private const int MaxFileNameLength = 255;

    public async Task<AnalysisOutcome> AnalyzeAsync(AnalysisInput input, CancellationToken cancellationToken)
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

        var stopwatch = Stopwatch.StartNew();
        var raw = await llm.CompleteAsync(
            Prompts.MatchSystem, Prompts.MatchUser(jobDescription, cvText), jsonMode: true, cancellationToken);
        var result = MatchResultParser.Parse(raw, cvText);

        return new AnalysisOutcome(
            Title: Truncate(FirstNonBlank(input.Title, result.JobTitle, "Untitled role"), MaxTitleLength),
            Company: result.Company is null ? null : Truncate(result.Company, MaxTitleLength),
            FileName: Truncate(Path.GetFileName(input.FileName), MaxFileNameLength),
            CvText: cvText,
            JobDescription: jobDescription,
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
