using FitCheck.Api.Infrastructure;
using FitCheck.Api.Services;

namespace FitCheck.Tests;

public class DocumentTextExtractorTests
{
    [Fact]
    public void Extracts_text_from_every_pdf_page()
    {
        var pdf = TestDocuments.Pdf(
            ["Jane Doe", "Senior QA Engineer"],
            ["Skills: Playwright, C#, SQL"]);

        var text = DocumentTextExtractor.Extract("cv.pdf", pdf, maxPdfPages: 10);

        Assert.Contains("Jane Doe", text);
        Assert.Contains("Senior QA Engineer", text);
        Assert.Contains("Playwright, C#, SQL", text);
    }

    [Fact]
    public void Extracts_docx_paragraphs_and_table_text()
    {
        var docx = TestDocuments.Docx(["Jane Doe", "Experience: 6 years in test automation"], ["Cypress", "Jenkins"]);

        var text = DocumentTextExtractor.Extract("cv.docx", docx, maxPdfPages: 10);

        Assert.Contains("Experience: 6 years in test automation", text);
        Assert.Contains("Cypress", text);
        Assert.Contains("Jenkins", text);
    }

    [Fact]
    public void Detects_type_from_content_not_just_extension()
    {
        var pdf = TestDocuments.Pdf(["Hello"]);

        Assert.Equal(DocumentKind.Pdf, DocumentTextExtractor.Detect("renamed.docx", pdf));
    }

    [Theory]
    [InlineData("cv.doc", "Legacy .doc")]
    [InlineData("cv.txt", "PDF or DOCX")]
    [InlineData("cv.png", "PDF or DOCX")]
    public void Rejects_unsupported_files(string fileName, string expectedMessage)
    {
        var ex = Assert.Throws<InputValidationException>(() =>
            DocumentTextExtractor.Extract(fileName, "just some text"u8.ToArray(), 10));

        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void Rejects_a_zip_that_is_not_a_word_document()
    {
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            zip.CreateEntry("hello.txt");

        var ex = Assert.Throws<InputValidationException>(() =>
            DocumentTextExtractor.Extract("cv.docx", stream.ToArray(), 10));

        Assert.Contains("Couldn't read this DOCX", ex.Message);
    }

    [Fact]
    public void Rejects_a_corrupt_pdf()
    {
        var ex = Assert.Throws<InputValidationException>(() =>
            DocumentTextExtractor.Extract("cv.pdf", "%PDF-1.7 garbage that is not a pdf"u8.ToArray(), 10));

        Assert.Contains("Couldn't read this PDF", ex.Message);
    }

    [Fact]
    public void Enforces_the_page_limit()
    {
        var pdf = TestDocuments.Pdf(["one"], ["two"], ["three"]);

        var ex = Assert.Throws<InputValidationException>(() => DocumentTextExtractor.Extract("cv.pdf", pdf, maxPdfPages: 2));

        Assert.Contains("3 pages", ex.Message);
    }

    [Fact]
    public void Normalizes_whitespace()
    {
        Assert.Equal("a b\n\nc", DocumentTextExtractor.NormalizeWhitespace("  a \t  b \r\n\r\n\r\n\r\n c  "));
    }
}
