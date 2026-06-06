using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PdfCore;

public static class PdfTextExtractor
{
    public static PdfDocumentModel ExtractTextModel(string pdfPath, PdfCheckResult check)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        var fullPath = Path.GetFullPath(pdfPath);

        try
        {
            using var document = PdfDocument.Open(fullPath);
            var model = new PdfDocumentModel
            {
                Source = new PdfSourceInfo
                {
                    Path = Path.GetFileName(pdfPath),
                    Sha256 = check.Sha256 ?? PdfUtilities.Sha256(fullPath),
                    PageCount = document.NumberOfPages,
                    Classification = check.Classification,
                },
                Warnings = new List<string>(check.Warnings),
            };

            var contentIndex = 0;
            var pageBreakIndex = 0;
            var headingIndex = 0;
            var paragraphIndex = 0;

            foreach (var page in document.GetPages())
            {
                model.Pages.Add(new PdfPageModel
                {
                    Page = page.Number,
                    Width = page.Width,
                    Height = page.Height,
                    TextCharCount = PdfDocumentInspector.CountMeaningfulChars(page.Text),
                    ImageCount = page.NumberOfImages,
                });

                if (page.Number > 1)
                {
                    pageBreakIndex++;
                    contentIndex++;
                    model.Blocks.Add(new PdfContentBlock
                    {
                        Id = $"pb{pageBreakIndex:D4}",
                        BlockId = $"pb{pageBreakIndex:D4}",
                        Index = contentIndex - 1,
                        Kind = "pageBreak",
                        Page = page.Number,
                        Bbox = [0, 0, page.Width, page.Height],
                        Source = "inferred",
                        Confidence = "high",
                    });
                }

                var lineGroups = BuildLineGroups(page);
                foreach (var line in lineGroups)
                {
                    var text = PdfUtilities.SanitizeText(string.Join(" ", line.Words.Select(w => w.Text)));
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    var kind = InferKind(line, text, paragraphIndex + headingIndex);
                    string id;
                    if (kind == "heading")
                    {
                        headingIndex++;
                        id = $"h{headingIndex:D4}";
                    }
                    else
                    {
                        paragraphIndex++;
                        id = $"p{paragraphIndex:D4}";
                    }

                    contentIndex++;
                    model.Blocks.Add(new PdfContentBlock
                    {
                        Id = id,
                        BlockId = id,
                        Index = contentIndex - 1,
                        Kind = kind,
                        Page = page.Number,
                        Bbox = ToBbox(line.BoundingBox),
                        Source = "pdfText",
                        Text = text,
                        Runs = BuildRuns(line.Words),
                        Format = new PdfBlockFormat
                        {
                            Font = MostCommonFont(line.Words),
                            Size = MedianPointSize(line.Words),
                            Align = InferAlignment(line.BoundingBox, page.Width),
                        },
                        Confidence = "medium",
                    });
                }
            }

            if (model.Blocks.All(b => b.Kind == "pageBreak"))
            {
                model.Warnings.Add("No extractable text blocks were found. Use --mode ocr when local OCR runtime is installed, or use ocr cloud/to-word when a cloud key exists.");
            }

            return model;
        }
        catch (PdfProcessingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfProcessingException(PdfErrorKind.ReadFailed, $"Failed to extract PDF text: {ex.Message}", ex);
        }
    }

    static List<PdfLineGroup> BuildLineGroups(Page page)
    {
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var lines = new List<PdfLineGroup>();
        foreach (var word in words)
        {
            var y = word.BoundingBox.Bottom;
            var tolerance = Math.Max(2.0, MedianLetterSize(word) * 0.45);
            var line = lines.FirstOrDefault(l => Math.Abs(l.BaselineY - y) <= tolerance);
            if (line == null)
            {
                line = new PdfLineGroup { BaselineY = y };
                lines.Add(line);
            }

            line.Words.Add(word);
        }

        foreach (var line in lines)
        {
            line.Words = line.Words.OrderBy(w => w.BoundingBox.Left).ToList();
            line.BoundingBox = Union(line.Words.Select(w => w.BoundingBox));
        }

        return lines
            .OrderByDescending(l => l.BoundingBox.Top)
            .ThenBy(l => l.BoundingBox.Left)
            .ToList();
    }

    static List<PdfRun> BuildRuns(List<Word> words) =>
        words.Select(w => new PdfRun
        {
            Text = w.Text,
            Bbox = ToBbox(w.BoundingBox),
            Format = new PdfRunFormat
            {
                Font = w.FontName,
                Size = MedianPointSize(w.Letters),
                Bold = LooksBold(w.FontName),
                Italic = LooksItalic(w.FontName),
            }
        }).ToList();

    static string InferKind(PdfLineGroup line, string text, int priorTextBlocks)
    {
        var size = MedianPointSize(line.Words);
        if (priorTextBlocks == 0 && text.Length <= 80)
            return "heading";
        if (size >= 15 && text.Length <= 100)
            return "heading";
        return "paragraph";
    }

    static string InferAlignment(PdfRectangle bbox, double pageWidth)
    {
        var center = (bbox.Left + bbox.Right) / 2.0;
        if (Math.Abs(center - pageWidth / 2.0) <= pageWidth * 0.08)
            return "center";
        if (bbox.Left <= pageWidth * 0.12)
            return "left";
        return "unknown";
    }

    static string? MostCommonFont(IEnumerable<Word> words) =>
        words.Select(w => w.FontName)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .GroupBy(f => f)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    static double? MedianPointSize(IEnumerable<Word> words) =>
        MedianPointSize(words.SelectMany(w => w.Letters));

    static double? MedianPointSize(IEnumerable<Letter> letters)
    {
        var sizes = letters
            .Select(l => l.PointSize > 0 ? l.PointSize : l.FontSize)
            .Where(double.IsFinite)
            .Where(s => s > 0)
            .OrderBy(s => s)
            .ToList();
        if (sizes.Count == 0) return null;
        return sizes[sizes.Count / 2];
    }

    static double MedianLetterSize(Word word) =>
        MedianPointSize(word.Letters) ?? Math.Max(1, word.BoundingBox.Height);

    static bool LooksBold(string? font) =>
        !string.IsNullOrWhiteSpace(font) && font.Contains("Bold", StringComparison.OrdinalIgnoreCase);

    static bool LooksItalic(string? font) =>
        !string.IsNullOrWhiteSpace(font) &&
        (font.Contains("Italic", StringComparison.OrdinalIgnoreCase) || font.Contains("Oblique", StringComparison.OrdinalIgnoreCase));

    static PdfRectangle Union(IEnumerable<PdfRectangle> boxes)
    {
        var list = boxes.ToList();
        if (list.Count == 0) return new PdfRectangle(0, 0, 0, 0);
        return new PdfRectangle(
            list.Min(b => b.Left),
            list.Min(b => b.Bottom),
            list.Max(b => b.Right),
            list.Max(b => b.Top));
    }

    internal static double[] ToBbox(PdfRectangle rectangle) =>
    [
        Math.Round(rectangle.Left, 3),
        Math.Round(rectangle.Bottom, 3),
        Math.Round(rectangle.Right, 3),
        Math.Round(rectangle.Top, 3),
    ];

    sealed class PdfLineGroup
    {
        public double BaselineY { get; set; }
        public List<Word> Words { get; set; } = new();
        public PdfRectangle BoundingBox { get; set; } = new(0, 0, 0, 0);
    }
}
