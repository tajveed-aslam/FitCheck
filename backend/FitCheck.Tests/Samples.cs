namespace FitCheck.Tests;

internal static class Samples
{
    public static readonly string[] CvLines =
    [
        "Jane Doe - Senior QA Engineer",
        "jane.doe@example.com | Berlin, Germany",
        "Summary: 7 years of test automation for web and API products.",
        "Skills: Playwright, TypeScript, C#, ASP.NET Core, REST APIs, SQL, Jenkins, CI/CD, Docker",
        "Experience: Senior QA Engineer, Globex (2021-present)",
        "- Built a Playwright + TypeScript framework covering 400 end-to-end tests",
        "- Cut regression time from 2 days to 3 hours by parallelising in Jenkins",
        "QA Engineer, Initech (2018-2021): API testing with Postman and pytest",
    ];

    public const string JobDescription = """
        Senior SDET - Acme Corp (Remote, EU)

        We're looking for a Senior SDET to own test automation for our payments platform.

        Requirements:
        - 5+ years of test automation experience
        - Strong Playwright or Cypress skills with TypeScript
        - API testing experience (REST APIs, Postman)
        - CI/CD pipelines (Jenkins or GitHub Actions)
        - Docker and Kubernetes

        Nice to have:
        - Performance testing with k6
        - AWS
        """;

    public const string ModelAnswer = """
        {
          "jobTitle": "Senior SDET",
          "company": "Acme Corp",
          "matchScore": 72,
          "summary": "Strong automation background with the exact UI and API stack. Gaps in container orchestration and performance testing.",
          "matchedSkills": ["Playwright", "TypeScript", "REST APIs", "Postman", "Jenkins"],
          "missingKeywords": [
            { "keyword": "k6", "importance": "medium" },
            { "keyword": "Kubernetes", "importance": "high" },
            { "keyword": "Docker", "importance": "high" },
            { "keyword": "CI/CD", "importance": "high" },
            { "keyword": "AWS", "importance": "low" }
          ],
          "tips": [
            { "title": "Quantify your impact", "detail": "Lead with the 2 days to 3 hours regression win." },
            { "title": "Surface API depth", "detail": "Expand the Postman/pytest work with numbers." },
            { "title": "Address Kubernetes", "detail": "Mention any container orchestration exposure." }
          ]
        }
        """;
}
