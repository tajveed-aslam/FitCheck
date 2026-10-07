using FitCheck.Api.Infrastructure;
using FitCheck.Api.Models;
using FitCheck.Api.Services;

namespace FitCheck.Tests;

public class MatchResultParserTests
{
    private const string Cv = "Senior QA engineer. Playwright, TypeScript, ASP.NET Core, CI/CD with Jenkins. Some Docker.";

    [Fact]
    public void Parses_a_well_formed_answer()
    {
        var result = MatchResultParser.Parse(Samples.ModelAnswer, Cv);

        Assert.Equal("Senior SDET", result.JobTitle);
        Assert.Equal("Acme Corp", result.Company);
        Assert.Equal(72, result.MatchScore);
        Assert.StartsWith("Strong automation", result.Summary);
        Assert.Equal(["Playwright", "TypeScript"], result.Details.MatchedSkills.Take(2));
        Assert.Equal(3, result.Details.Tips.Count);
        Assert.Equal("Quantify your impact", result.Details.Tips[0].Title);
    }

    [Fact]
    public void Moves_missing_keywords_that_the_cv_actually_contains_to_matched()
    {
        var result = MatchResultParser.Parse(Samples.ModelAnswer, Cv);

        // The model claimed "Docker" and "CI/CD" were missing, but the CV says both.
        Assert.Contains("Docker", result.Details.MatchedSkills);
        Assert.Contains("CI/CD", result.Details.MatchedSkills);
        Assert.DoesNotContain(result.Details.MissingKeywords, m => m.Keyword is "Docker" or "CI/CD");
        Assert.Contains(result.Details.MissingKeywords, m => m.Keyword == "Kubernetes" && m.Importance == Importance.High);
    }

    [Fact]
    public void Orders_missing_keywords_by_importance()
    {
        var result = MatchResultParser.Parse(Samples.ModelAnswer, Cv);

        Assert.Equal(
            result.Details.MissingKeywords.OrderBy(m => m.Importance).Select(m => m.Keyword),
            result.Details.MissingKeywords.Select(m => m.Keyword));
    }

    [Theory]
    [InlineData("150", 100)]
    [InlineData("-5", 0)]
    [InlineData("\"64%\"", 64)]
    [InlineData("71.6", 72)]
    public void Clamps_and_normalizes_the_score(string scoreJson, int expected)
    {
        var json = $$"""{ "matchScore": {{scoreJson}}, "tips": [{ "title": "t", "detail": "d" }] }""";

        Assert.Equal(expected, MatchResultParser.Parse(json, Cv).MatchScore);
    }

    [Fact]
    public void Accepts_plain_strings_dedupes_and_keeps_at_most_three_tips()
    {
        const string json = """
            {
              "matchScore": 50,
              "company": "null",
              "matchedSkills": ["Playwright", "playwright", "  ", "TypeScript"],
              "missingKeywords": ["Go", "go", { "keyword": "Terraform", "importance": "LOW" }],
              "tips": ["one", "two", { "title": "three", "detail": "x" }, "four"]
            }
            """;

        var result = MatchResultParser.Parse(json, Cv);

        Assert.Null(result.Company);
        Assert.Equal(["Playwright", "TypeScript"], result.Details.MatchedSkills);
        Assert.Equal(["Go", "Terraform"], result.Details.MissingKeywords.Select(m => m.Keyword));
        Assert.Equal(Importance.Medium, result.Details.MissingKeywords[0].Importance);
        Assert.Equal(3, result.Details.Tips.Count);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "tips": [{ "title": "t" }] }""")]
    [InlineData("""{ "matchScore": 80, "tips": [] }""")]
    public void Throws_when_the_answer_is_unusable(string raw)
    {
        Assert.Throws<LlmException>(() => MatchResultParser.Parse(raw, Cv));
    }

    [Theory]
    [InlineData("Java", "Senior JavaScript developer", false)]
    [InlineData("Java", "Java 17, Spring", true)]
    [InlineData(".NET", "Built APIs in ASP.NET Core", true)]
    [InlineData("C#", "Languages: C#, SQL", true)]
    [InlineData("CI/CD", "owned the ci/cd pipeline", true)]
    [InlineData("REST APIs", "Designed REST   APIs", true)]
    [InlineData("Go", "Going forward", false)]
    public void Term_matching_respects_word_boundaries(string term, string text, bool expected)
    {
        Assert.Equal(expected, MatchResultParser.ContainsTerm(text, term));
    }
}
