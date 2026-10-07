using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FitCheck.Api.Data;
using FitCheck.Api.Infrastructure;
using FitCheck.Api.Models;
using FitCheck.Api.Options;
using FitCheck.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FitCheck.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/analyses")]
public sealed class AnalysesController(AppDbContext db, IAnalysisService analyzer, IOptions<AnalysisOptions> options)
    : ControllerBase
{
    private const int HistoryLimit = 100;
    // Generous outer cap for the multipart body; the per-file limit is enforced from AnalysisOptions.
    private const long MaxRequestBytes = 12 * 1024 * 1024;

    private Guid UserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimiting.AnalysisPolicy)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<AnalysisDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<AnalysisDto>> Create([FromForm] AnalyzeRequest request, CancellationToken cancellationToken)
    {
        var file = request.Cv!;
        var maxBytes = options.Value.MaxFileBytes;
        if (file.Length > maxBytes)
            throw new InputValidationException($"The CV must be smaller than {maxBytes / (1024 * 1024)} MB.");

        byte[] content;
        using (var buffer = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        var outcome = await analyzer.AnalyzeAsync(
            new AnalysisInput(file.FileName, content, request.JobDescription, request.Title), cancellationToken);

        var analysis = new Analysis
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = outcome.Title,
            Company = outcome.Company,
            FileName = outcome.FileName,
            CvText = outcome.CvText,
            JobDescription = outcome.JobDescription,
            MatchScore = outcome.Result.MatchScore,
            Summary = outcome.Result.Summary,
            DetailsJson = JsonSerializer.Serialize(outcome.Result.Details, JsonDefaults.Options),
            Model = outcome.Model,
            DurationMs = outcome.DurationMs,
            CreatedAt = DateTime.UtcNow,
        };
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = analysis.Id }, ToDto(analysis));
    }

    [HttpGet]
    public async Task<IReadOnlyList<AnalysisSummaryDto>> List(CancellationToken cancellationToken) =>
        await db.Analyses.AsNoTracking()
            .Where(a => a.UserId == UserId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(HistoryLimit)
            .Select(a => new AnalysisSummaryDto(a.Id, a.Title, a.Company, a.MatchScore, a.FileName, a.CreatedAt))
            .ToListAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AnalysisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var analysis = await db.Analyses.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == UserId, cancellationToken);
        return analysis is null ? NotFound() : ToDto(analysis);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.Analyses
            .Where(a => a.Id == id && a.UserId == UserId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFound() : NoContent();
    }

    private static AnalysisDto ToDto(Analysis a)
    {
        var details = JsonSerializer.Deserialize<MatchDetails>(a.DetailsJson, JsonDefaults.Options)
            ?? new MatchDetails([], [], []);
        return new AnalysisDto(
            a.Id, a.Title, a.Company, a.FileName, a.MatchScore, a.Summary,
            details.MatchedSkills, details.MissingKeywords, details.Tips,
            a.JobDescription, a.CvText, a.Model, a.DurationMs, a.CreatedAt);
    }
}
