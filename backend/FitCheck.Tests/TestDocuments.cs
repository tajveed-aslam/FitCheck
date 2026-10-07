using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace FitCheck.Tests;

/// <summary>Builds real PDF and DOCX files in memory so extraction is tested against genuine documents.</summary>
internal static class TestDocuments
{
    public static byte[] Pdf(params string[][] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var lines in pages)
        {
            var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            var y = 800d;
            foreach (var line in lines)
            {
                page.AddText(line, 11, new UglyToad.PdfPig.Core.PdfPoint(50, y), font);
                y -= 16;
            }
        }
        return builder.Build();
    }

    public static byte[] Docx(IEnumerable<string> paragraphs, IEnumerable<string>? tableCells = null)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body();
            foreach (var text in paragraphs)
                body.Append(new Paragraph(new Run(new Text(text))));

            if (tableCells is not null)
            {
                var row = new TableRow();
                foreach (var cell in tableCells)
                    row.Append(new TableCell(new Paragraph(new Run(new Text(cell)))));
                body.Append(new Table(row));
            }

            main.Document = new Document(body);
            main.Document.Save();
        }
        return stream.ToArray();
    }
}
