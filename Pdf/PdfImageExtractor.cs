using UglyToad.PdfPig;

namespace PdfCore;

public static class PdfImageExtractor
{
    const int FallbackCropDpi = 300;

    public static PdfImageExtractResult Extract(string pdfPath, string outputDir)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        Directory.CreateDirectory(outputDir);

        try
        {
            using var document = PdfDocument.Open(pdfPath);
            var result = new PdfImageExtractResult
            {
                OutputDir = Path.GetFullPath(outputDir),
                PageCount = document.NumberOfPages,
            };

            var index = 0;
            foreach (var page in document.GetPages())
            {
                foreach (var image in page.GetImages())
                {
                    index++;
                    var id = $"img{index:D4}";
                    var fileName = $"{id}.png";
                    var outPath = Path.Combine(outputDir, fileName);
                    string? warning = null;
                    var extractionMethod = "embeddedImage";

                    if (image.TryGetPng(out var pngBytes))
                    {
                        File.WriteAllBytes(outPath, pngBytes);
                    }
                    else
                    {
                        if (TrySavePageCropFallback(pdfPath, outputDir, page, image.BoundingBox, id, outPath, out _))
                        {
                            warning = "Image could not be decoded as PNG; page crop fallback saved.";
                            extractionMethod = "pageCrop";
                        }
                        else if (image.TryGetBytesAsMemory(out var rawMemory))
                        {
                            fileName = $"{id}.bin";
                            outPath = Path.Combine(outputDir, fileName);
                            File.WriteAllBytes(outPath, rawMemory.ToArray());
                            warning = "Image could not be decoded as PNG and page crop fallback failed; raw bytes were preserved.";
                            extractionMethod = "embeddedImageRaw";
                        }
                        else
                        {
                            warning = "Image bytes could not be decoded or extracted.";
                            result.Warnings.Add($"Page {page.Number} image {id}: {warning}");
                            continue;
                        }
                    }

                    var entry = new PdfAssetEntry
                    {
                        Id = id,
                        Path = fileName,
                        ContentType = Path.GetExtension(fileName).Equals(".png", StringComparison.OrdinalIgnoreCase)
                            ? "image/png"
                            : "application/octet-stream",
                        Size = new FileInfo(outPath).Length,
                        Page = page.Number,
                        Bbox = PdfTextExtractor.ToBbox(image.BoundingBox),
                        ExtractionMethod = extractionMethod,
                    };
                    if (warning != null)
                    {
                        entry.Warnings.Add(warning);
                        result.Warnings.Add($"Page {page.Number} image {id}: {warning}");
                    }
                    result.Items.Add(entry);
                }
            }

            result.ImageCount = result.Items.Count;
            var manifest = new PdfAssetManifest
            {
                Source = Path.GetFileName(pdfPath),
                Items = result.Items
            };
            PdfUtilities.WriteJson(Path.Combine(outputDir, "manifest.json"), manifest);
            return result;
        }
        catch (PdfProcessingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfProcessingException(PdfErrorKind.ReadFailed, $"Failed to extract PDF images: {ex.Message}", ex);
        }
    }

    static bool TrySavePageCropFallback(
        string pdfPath,
        string outputDir,
        UglyToad.PdfPig.Content.Page page,
        UglyToad.PdfPig.Core.PdfRectangle bbox,
        string id,
        string outPath,
        out string warning)
    {
        try
        {
            PdfPageRenderer.RenderCrop(
                pdfPath,
                page.Number,
                page.Width,
                page.Height,
                PdfTextExtractor.ToBbox(bbox),
                outPath,
                FallbackCropDpi);
            warning = "";
            return true;
        }
        catch (PdfProcessingException ex)
        {
            warning = $"Page crop fallback failed for {id}: {ex.Message}";
            TryDeletePartialFile(outPath);
            return false;
        }
        catch (Exception ex)
        {
            warning = $"Page crop fallback failed for {id}: {ex.Message}";
            TryDeletePartialFile(outPath);
            return false;
        }
    }

    static void TryDeletePartialFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only; the warning above is the actionable signal.
        }
    }
}
