using MultiModalCore;
using PdfCore;

namespace Nong.Cli.Adapters;

public sealed class PdfOcrRecognizerAdapter : IPdfOcrRecognizer
{
    public PdfOcrRecognizeResult Recognize(string imagePath, int pageNumber)
    {
        using var client = new PpOcrV5Client();
        var result = client.RecognizeAsync(imagePath).GetAwaiter().GetResult();
        var page = result.Pages.FirstOrDefault();

        var output = new PdfOcrRecognizeResult
        {
            Page = pageNumber,
            Width = page?.Width ?? 0,
            Height = page?.Height ?? 0,
            Engine = result.Engine,
            ModelId = result.ModelId,
        };

        if (result.NumericFallbackAttempted)
        {
            output.Warnings.Add(result.NumericFallbackApplied
                ? "Fast local OCR inference produced invalid numeric values; conservative CPU/BLAS fallback was applied."
                : "Fast local OCR inference produced invalid numeric values; conservative fallback was attempted but fast result was retained.");
        }

        if (page == null)
            return output;

        foreach (var block in page.Blocks)
        {
            var bbox = block.Bbox
                .Where(float.IsFinite)
                .Select(v => (double)v)
                .ToArray();

            output.Blocks.Add(new PdfOcrRecognizedBlock
            {
                Id = block.Id,
                Text = block.Text,
                Confidence = block.Confidence,
                Bbox = bbox,
                ConfidenceValid = block.ConfidenceValid,
                GeometryValid = block.GeometryValid && bbox.Length >= 4,
                NumericIssue = block.NumericIssue,
            });
        }

        return output;
    }
}
