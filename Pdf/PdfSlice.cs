using System.Text;
using System.Text.Json;

namespace PdfCore;

public interface IPdfOcrRecognizer
{
    PdfOcrRecognizeResult Recognize(string imagePath, int pageNumber);
}

public sealed record PdfOcrRecognizeResult
{
    public int Page { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Engine { get; set; } = "";
    public string ModelId { get; set; } = "";
    public List<PdfOcrRecognizedBlock> Blocks { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed record PdfOcrRecognizedBlock
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public double? Confidence { get; set; }
    public double[] Bbox { get; set; } = Array.Empty<double>();
    public bool ConfidenceValid { get; set; } = true;
    public bool GeometryValid { get; set; } = true;
    public string? NumericIssue { get; set; }
}

public static class PdfSlice
{
    public static PdfSliceResult Dissect(
        string pdfPath,
        string outputDir,
        PdfSliceOptions options,
        IPdfOcrRecognizer? ocrRecognizer = null)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        ValidateOptions(options);
        PrepareOutputDirectory(outputDir);

        var check = PdfDocumentInspector.Check(pdfPath);
        var effectiveMode = ResolveMode(options.Mode, check);
        var warnings = new List<string>(check.Warnings);

        if (effectiveMode == "text" && check.TextCharCount == 0)
        {
            throw new PdfProcessingException(
                PdfErrorKind.ValidationFailed,
                "Text mode requested, but this PDF has no useful text layer. Use --mode ocr with local OCR runtime installed, or cloud OCR for full layout parsing.");
        }

        PdfDocumentModel model;
        if (effectiveMode == "ocr")
        {
            model = BuildOcrModel(pdfPath, outputDir, check, options, ocrRecognizer, warnings);
        }
        else
        {
            model = PdfTextExtractor.ExtractTextModel(pdfPath, check);
            model.Warnings.AddRange(warnings.Where(w => !model.Warnings.Contains(w)));

            if (effectiveMode == "hybrid")
            {
                model.Warnings.Add("Hybrid mode currently preserves native PDF text and embedded image evidence; image-region OCR will be expanded in the next layout pass.");
            }
        }

        var assets = PdfImageExtractor.Extract(pdfPath, Path.Combine(outputDir, "assets"));
        model.Assets = assets.Items;
        model.Warnings.AddRange(assets.Warnings);
        AddImageBlocks(model, assets.Items);
        ReindexBlocks(model.Blocks);

        WriteSliceFiles(pdfPath, outputDir, model, check);
        VerifyCriticalArtifacts(outputDir);

        return new PdfSliceResult
        {
            OutputDir = Path.GetFullPath(outputDir),
            ManifestPath = Path.GetFullPath(Path.Combine(outputDir, "manifest.json")),
            BlockCount = model.Blocks.Count,
            AssetCount = model.Assets.Count,
            PageCount = model.Pages.Count,
            Classification = check.Classification,
            Warnings = model.Warnings.Distinct().ToList(),
        };
    }

    static void ValidateOptions(PdfSliceOptions options)
    {
        var mode = options.Mode.ToLowerInvariant();
        if (mode is not ("auto" or "text" or "hybrid" or "ocr"))
        {
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "Unsupported --mode. Supported: auto, text, hybrid, ocr.");
        }

        if (options.Dpi is < 72 or > 600)
        {
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "DPI must be between 72 and 600.");
        }
    }

    static string ResolveMode(string mode, PdfCheckResult check)
    {
        mode = mode.ToLowerInvariant();
        if (mode != "auto") return mode;
        return check.Classification switch
        {
            "scan" => "ocr",
            "hybrid" => "hybrid",
            _ => "text"
        };
    }

    static PdfDocumentModel BuildOcrModel(
        string pdfPath,
        string outputDir,
        PdfCheckResult check,
        PdfSliceOptions options,
        IPdfOcrRecognizer? ocrRecognizer,
        List<string> warnings)
    {
        if (ocrRecognizer == null)
        {
            throw new PdfProcessingException(
                PdfErrorKind.DependencyMissing,
                "PDF OCR mode requires local PP-OCRv5 runtime. Run 'nong ocr install-model pp-ocrv5-mobile --json', then rerun pdf dissect --mode ocr. No Python is required.");
        }

        var pagesDir = Path.Combine(outputDir, "pages");
        var render = PdfPageRenderer.Render(pdfPath, pagesDir, options.Dpi);
        var ocrDir = Path.Combine(outputDir, "ocr");
        Directory.CreateDirectory(ocrDir);

        var model = new PdfDocumentModel
        {
            Source = new PdfSourceInfo
            {
                Path = Path.GetFileName(pdfPath),
                Sha256 = check.Sha256 ?? PdfUtilities.Sha256(pdfPath),
                PageCount = check.PageCount,
                Classification = check.Classification,
            },
            Warnings = warnings,
        };

        var blockIndex = 0;
        var ocrBlockIndex = 0;
        using var pagesWriter = new StreamWriter(Path.Combine(ocrDir, "pages.jsonl"), false, Encoding.UTF8);
        using var blocksWriter = new StreamWriter(Path.Combine(ocrDir, "blocks.jsonl"), false, Encoding.UTF8);

        foreach (var page in render.Pages)
        {
            model.Pages.Add(new PdfPageModel
            {
                Page = page.Page,
                Width = page.Width,
                Height = page.Height,
                Unit = "px",
                TextCharCount = 0,
                ImageCount = 1,
            });

            var pageImage = Path.Combine(pagesDir, page.Path);
            var ocr = ocrRecognizer.Recognize(pageImage, page.Page);
            foreach (var warning in ocr.Warnings)
                model.Warnings.Add(warning);

            pagesWriter.WriteLine(JsonSerializer.Serialize(ocr, PdfUtilities.JsonlOpts));

            foreach (var ocrBlock in ocr.Blocks.Where(b => !string.IsNullOrWhiteSpace(b.Text)))
            {
                ocrBlockIndex++;
                blockIndex++;
                var id = $"ocr{ocrBlockIndex:D4}";
                var block = new PdfContentBlock
                {
                    Id = id,
                    BlockId = id,
                    Index = blockIndex - 1,
                    Kind = "ocrText",
                    Page = page.Page,
                    Bbox = ocrBlock.Bbox,
                    Source = "localOcr",
                    Text = ocrBlock.Text,
                    Confidence = ocrBlock.GeometryValid ? "medium" : "low",
                };
                if (!ocrBlock.ConfidenceValid)
                    block.Warnings.Add("OCR confidence was invalid and serialized as null.");
                if (!ocrBlock.GeometryValid)
                    block.Warnings.Add("OCR geometry was invalid or incomplete.");

                model.Blocks.Add(block);
                blocksWriter.WriteLine(JsonSerializer.Serialize(block, PdfUtilities.JsonlOpts));
            }
        }

        if (model.Blocks.Count == 0)
        {
            throw new PdfProcessingException(
                PdfErrorKind.ReadFailed,
                "Local OCR returned no text blocks; Nong will not publish an empty PDF slice as success. Try a higher --dpi value, verify the page render output, or use cloud OCR for layout-heavy PDFs.");
        }

        return model;
    }

    static void AddImageBlocks(PdfDocumentModel model, List<PdfAssetEntry> assets)
    {
        var imageIndex = 0;
        foreach (var asset in assets)
        {
            imageIndex++;
            var id = asset.Id.Length > 0 ? asset.Id : $"img{imageIndex:D4}";
            model.Blocks.Add(new PdfContentBlock
            {
                Id = id,
                BlockId = id,
                Index = model.Blocks.Count,
                Kind = "image",
                Page = asset.Page,
                Bbox = asset.Bbox,
                Source = asset.ExtractionMethod switch
                {
                    "embeddedImage" => "pdfImage",
                    "pageCrop" => "pageCrop",
                    "embeddedImageRaw" => "pdfImageRaw",
                    _ => "inferred",
                },
                Text = asset.Caption ?? asset.AltTextCandidate ?? "image",
                AssetId = asset.Id,
                AssetPath = "assets/" + asset.Path.Replace('\\', '/'),
                CaptionBlockId = asset.CaptionBlockId,
                OcrBlockIds = asset.OcrBlockIds,
                Confidence = "medium",
                Warnings = asset.Warnings,
            });
        }
    }

    static void ReindexBlocks(List<PdfContentBlock> blocks)
    {
        var ordered = blocks
            .OrderBy(b => b.Page)
            .ThenBy(b => b.Index)
            .ToList();

        blocks.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Index = i;
            ordered[i].BlockId = string.IsNullOrWhiteSpace(ordered[i].BlockId) ? ordered[i].Id : ordered[i].BlockId;
            blocks.Add(ordered[i]);
        }
    }

    static void WriteSliceFiles(string pdfPath, string outputDir, PdfDocumentModel model, PdfCheckResult check)
    {
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(Path.Combine(outputDir, "preview"));
        Directory.CreateDirectory(Path.Combine(outputDir, "diagnostics"));
        Directory.CreateDirectory(Path.Combine(outputDir, "assets"));
        Directory.CreateDirectory(Path.Combine(outputDir, "ocr"));

        var metrics = ComputeMetrics(model);
        var manifest = new PdfManifest
        {
            Source = model.Source,
            CreatedAt = DateTime.UtcNow,
            Metrics = metrics,
            Warnings = model.Warnings.Distinct().ToList(),
        };
        PdfUtilities.WriteJson(Path.Combine(outputDir, "manifest.json"), manifest);
        PdfUtilities.WriteJson(Path.Combine(outputDir, "document.json"), model);

        using (var sw = new StreamWriter(Path.Combine(outputDir, "content.jsonl"), false, Encoding.UTF8))
        {
            foreach (var block in model.Blocks.OrderBy(b => b.Index))
            {
                sw.WriteLine(JsonSerializer.Serialize(block, PdfUtilities.JsonlOpts));
            }
        }

        var structure = BuildStructure(Path.GetFileName(pdfPath), model);
        PdfUtilities.WriteJson(Path.Combine(outputDir, "structure.json"), structure);
        PdfUtilities.WriteJson(Path.Combine(outputDir, "format.json"), BuildFormat(Path.GetFileName(pdfPath), model));

        File.WriteAllText(Path.Combine(outputDir, "content.nongmark"), PdfNongMarkTextWriter.Write(model), Encoding.UTF8);
        File.WriteAllText(Path.Combine(outputDir, "preview", "content.md"), PdfMarkdownPreviewWriter.Write(model), Encoding.UTF8);
        PdfUtilities.WriteJson(Path.Combine(outputDir, "diagnostics", "check.json"), check);
        PdfUtilities.WriteJson(Path.Combine(outputDir, "diagnostics", "reading-order.json"), PdfReadingOrder.BuildDiagnostics(model));
        PdfUtilities.WriteJson(Path.Combine(outputDir, "diagnostics", "warnings.json"), model.Warnings.Distinct().ToList());
        WriteIfMissing(Path.Combine(outputDir, "assets", "manifest.json"), new PdfAssetManifest { Source = Path.GetFileName(pdfPath), Items = model.Assets });
    }

    static PdfSliceMetrics ComputeMetrics(PdfDocumentModel model) => new()
    {
        Pages = model.Pages.Count,
        Blocks = model.Blocks.Count,
        Paragraphs = model.Blocks.Count(b => b.Kind == "paragraph"),
        Headings = model.Blocks.Count(b => b.Kind == "heading"),
        Images = model.Blocks.Count(b => b.Kind == "image"),
        OcrTextBlocks = model.Blocks.Count(b => b.Kind == "ocrText"),
        Tables = model.Blocks.Count(b => b.Kind == "table"),
        Warnings = model.Warnings.Distinct().Count(),
    };

    static PdfStructure BuildStructure(string source, PdfDocumentModel model)
    {
        var structure = new PdfStructure { Source = source };
        foreach (var block in model.Blocks.OrderBy(b => b.Index))
        {
            structure.BlockIndex[block.BlockId] = new PdfBlockIndexEntry
            {
                Kind = block.Kind,
                Order = block.Index,
                Page = block.Page,
                TextPreview = PdfUtilities.Preview(block.Text),
                Bbox = block.Bbox,
                Source = block.Source,
            };

            var page = structure.Pages.FirstOrDefault(p => p.Page == block.Page);
            if (page == null)
            {
                page = new PdfPageStructure { Page = block.Page };
                structure.Pages.Add(page);
            }
            page.BlockIds.Add(block.BlockId);

            if (block.Kind == "heading")
            {
                structure.Outline.Add(new PdfOutlineItem
                {
                    Id = block.BlockId,
                    Text = block.Text ?? "",
                    Page = block.Page,
                    Level = 1,
                });
            }
        }

        if (model.Source.Classification is "hybrid" or "scan")
            structure.Issues.Add("Layout/reading-order confidence is limited for image-heavy PDFs.");
        return structure;
    }

    static PdfFormatDocument BuildFormat(string source, PdfDocumentModel model)
    {
        var format = new PdfFormatDocument
        {
            Source = source,
            Warnings = model.Warnings.Distinct().ToList(),
        };

        foreach (var page in model.Pages)
        {
            var fonts = model.Blocks
                .Where(b => b.Page == page.Page)
                .SelectMany(b => b.Runs)
                .Select(r => r.Format?.Font)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct()
                .OrderBy(f => f)
                .Cast<string>()
                .ToList();
            format.Pages.Add(new PdfPageFormat
            {
                Page = page.Page,
                Width = page.Width,
                Height = page.Height,
                Unit = page.Unit,
                Fonts = fonts,
            });
            format.Fonts.AddRange(fonts);
        }

        format.Fonts = format.Fonts.Distinct().OrderBy(f => f).ToList();
        return format;
    }

    static void PrepareOutputDirectory(string outputDir)
    {
        if (string.IsNullOrWhiteSpace(outputDir))
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, "Output directory is required.");

        var full = Path.GetFullPath(outputDir);
        if (File.Exists(full))
            throw new PdfProcessingException(PdfErrorKind.ValidationFailed, $"Output path is a file, not a directory: {outputDir}");

        Directory.CreateDirectory(full);
    }

    static void WriteIfMissing(string path, object value)
    {
        if (!File.Exists(path))
            PdfUtilities.WriteJson(path, value);
    }

    static void VerifyCriticalArtifacts(string outputDir)
    {
        var required = new[]
        {
            "manifest.json",
            "document.json",
            "content.jsonl",
            "structure.json",
            "format.json",
            "content.nongmark",
            Path.Combine("assets", "manifest.json")
        };

        foreach (var relative in required)
        {
            var path = Path.Combine(outputDir, relative);
            if (!File.Exists(path))
                throw new PdfProcessingException(PdfErrorKind.WriteFailed, $"Required artifact was not created: {relative}");
            if (new FileInfo(path).Length == 0)
                throw new PdfProcessingException(PdfErrorKind.WriteFailed, $"Required artifact is empty: {relative}");
        }
    }
}
