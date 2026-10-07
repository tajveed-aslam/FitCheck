using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FitCheck.Api.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace FitCheck.Api.Services;

public enum DocumentKind
{
    Pdf,
    Docx,
}

/// <summary>Pulls plain text out of an uploaded CV. The file type is decided by its bytes, not just its name.</summary>
public static partial class DocumentTextExtractor
{
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    public static string Extract(string fileName, byte[] content, int maxPdfPages)
    {
        var raw = Detect(fileName, content) switch
        {
            DocumentKind.Pdf => ExtractPdf(content, maxPdfPages),
            _ => ExtractDocx(content),
        };
        return NormalizeWhitespace(raw);
    }

    public static DocumentKind Detect(string fileName, byte[] content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".doc")
            throw new InputValidationException("Legacy .doc files aren't supported. Save the CV as .docx or PDF and try again.");

        if (content.AsSpan().StartsWith(PdfMagic))
            return DocumentKind.Pdf;

        // A .docx is a zip package; other zip-based files fail later when opened as Word documents.
        if (content.AsSpan().StartsWith(ZipMagic) && extension == ".docx")
            return DocumentKind.Docx;

        throw new InputValidationException("Upload your CV as a PDF or DOCX file.");
    }

    private static string ExtractPdf(byte[] content, int maxPages)
    {
        try
        {
            using var document = PdfDocument.Open(content);
            if (document.NumberOfPages > maxPages)
                throw new InputValidationException(
                    $"This PDF has {document.NumberOfPages} pages; CVs up to {maxPages} pages are supported.");

            var text = new StringBuilder();
            foreach (var page in document.GetPages())
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
            return text.ToString();
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new InputValidationException("This PDF is password-protected. Remove the password and upload it again.");
        }
        catch (Exception ex) when (ex is not InputValidationException)
        {
            throw new InputValidationException("Couldn't read this PDF. It may be damaged; try exporting it again.");
        }
    }

    private static string ExtractDocx(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var document = WordprocessingDocument.Open(stream, isEditable: false);
            var body = document.MainDocumentPart?.Document?.Body
                ?? throw new InvalidDataException("Package has no Word document body.");

            // Descendants (not just direct children) so text inside tables is included too.
            return string.Join('\n', body.Descendants<Paragraph>().Select(p => p.InnerText));
        }
        catch (Exception ex) when (ex is not InputValidationException)
        {
            throw new InputValidationException("Couldn't read this DOCX file. It may be damaged; try saving it again.");
        }
    }

    public static string NormalizeWhitespace(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(line => InlineWhitespace().Replace(line, " ").Trim());
        return ExtraBlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    [GeneratedRegex(@"[ \t\f\v ]+")]
    private static partial Regex InlineWhitespace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLines();
}
