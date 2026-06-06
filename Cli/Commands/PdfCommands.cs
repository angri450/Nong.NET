using System.CommandLine;
using System.Text.Json;
using Nong.Cli.Adapters;
using Nong.Cli.Common;
using PdfCore;

namespace Nong.Cli.Commands;

public static class PdfCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("pdf", "PDF document parsing operations");
        cmd.AddCommand(CreateCheck(jsonOpt));
        cmd.AddCommand(CreateDissect(jsonOpt));
        cmd.AddCommand(CreateRender(jsonOpt));
        cmd.AddCommand(CreateImages(jsonOpt));
        return cmd;
    }

    static Command CreateCheck(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var cmd = new Command("check", "Preflight PDF and classify text/hybrid/scan route") { fileArg };
        cmd.SetHandler((string file, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfDocumentInspector.Check(file));
                var output = JsonOutput.Ok("pdf check",
                    $"PDF preflight: {result.Classification}, {result.PageCount} page(s), {result.Warnings.Count} warning(s)",
                    result);
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["textChars"] = result.TextCharCount;
                output.Metrics["images"] = result.ImageCount;
                output.Metrics["renderRequired"] = result.RenderRequired ? 1 : 0;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_preflight");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf check", ex, json);
            }
        }, fileArg, jsonOpt);
        return cmd;
    }

    static Command CreateDissect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory for PDF one-cut three-stream slice") { IsRequired = true };
        var modeOpt = new Option<string>("--mode", () => "auto", "Mode: auto, text, hybrid, ocr");
        var dpiOpt = new Option<int>("--dpi", () => 200, "Render DPI for OCR mode");
        var cmd = new Command("dissect", "Slice PDF into nongpdf/nongmark streams") { fileArg, outOpt, modeOpt, dpiOpt };

        cmd.SetHandler((string file, string outputDir, string mode, int dpi, bool json) =>
        {
            try
            {
                var options = new PdfSliceOptions { Mode = mode, Dpi = dpi };
                var recognizer = ShouldProvideOcr(file, mode) ? new PdfOcrRecognizerAdapter() : null;
                var (result, elapsed) = CliHelpers.Time(() => PdfSlice.Dissect(file, outputDir, options, recognizer));
                var output = JsonOutput.Ok("pdf dissect",
                    $"PDF slice: {result.BlockCount} block(s), {result.AssetCount} asset(s), {result.Warnings.Count} warning(s)",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Artifacts["nongmark"] = Path.Combine(result.OutputDir, "content.nongmark");
                output.Artifacts["contentJsonl"] = Path.Combine(result.OutputDir, "content.jsonl");
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["blocks"] = result.BlockCount;
                output.Metrics["assets"] = result.AssetCount;
                output.Metrics["warnings"] = result.Warnings.Count;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_slice");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf dissect", ex, json);
            }
        }, fileArg, outOpt, modeOpt, dpiOpt, jsonOpt);
        return cmd;
    }

    static Command CreateRender(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output page image directory") { IsRequired = true };
        var dpiOpt = new Option<int>("--dpi", () => 200, "Render DPI");
        var cmd = new Command("render", "Render PDF pages to PNG images") { fileArg, outOpt, dpiOpt };

        cmd.SetHandler((string file, string outputDir, int dpi, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfPageRenderer.Render(file, outputDir, dpi));
                var output = JsonOutput.Ok("pdf render",
                    $"Rendered {result.PageCount} page(s) at {dpi} DPI",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["dpi"] = result.Dpi;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf render", ex, json);
            }
        }, fileArg, outOpt, dpiOpt, jsonOpt);
        return cmd;
    }

    static Command CreateImages(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output assets directory") { IsRequired = true };
        var cmd = new Command("images", "Extract embedded PDF images and write provenance manifest") { fileArg, outOpt };

        cmd.SetHandler((string file, string outputDir, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfImageExtractor.Extract(file, outputDir));
                var output = JsonOutput.Ok("pdf images",
                    $"Extracted {result.ImageCount} image(s)",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Artifacts["manifest"] = Path.Combine(result.OutputDir, "manifest.json");
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["images"] = result.ImageCount;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_image");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf images", ex, json);
            }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static bool ShouldProvideOcr(string file, string mode)
    {
        if (mode.Equals("ocr", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!mode.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var check = PdfDocumentInspector.Check(file);
            return check.Classification == "scan";
        }
        catch
        {
            return false;
        }
    }

    static void AddWarnings(JsonOutput output, IEnumerable<string> warnings, string id)
    {
        foreach (var warning in warnings)
        {
            output.Issues.Add(new Issue
            {
                Id = id,
                Severity = "Warning",
                Message = warning
            });
        }
    }

    static void WritePdfError(string command, Exception ex, bool json)
    {
        if (ex is AggregateException ae && ae.InnerException != null)
            ex = ae.InnerException;

        if (ex is PdfProcessingException pdfEx)
        {
            CliHelpers.WriteError(command, ToErrorEntry(pdfEx), json);
            return;
        }

        if (IsLocalOcrDependencyException(ex))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.DependencyMissing with
                {
                    Message = $"Local OCR/PDF native dependency is unavailable: {ex.Message}. Run 'nong ocr install-model pp-ocrv5-mobile --json' for OCR mode. No Python is required."
                }, json);
            return;
        }

        CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
    }

    static ErrorEntry ToErrorEntry(PdfProcessingException ex)
    {
        var entry = ex.Kind switch
        {
            PdfErrorKind.FileNotFound => ErrorCodes.FileNotFound,
            PdfErrorKind.UnsupportedFormat => ErrorCodes.UnsupportedFormat,
            PdfErrorKind.DependencyMissing => ErrorCodes.DependencyMissing,
            PdfErrorKind.ValidationFailed => ErrorCodes.ValidationFailed,
            PdfErrorKind.ReadFailed => ErrorCodes.ReadFailed,
            PdfErrorKind.WriteFailed => ErrorCodes.WriteFailed,
            _ => ErrorCodes.InternalError,
        };
        return entry with { Message = ex.Message };
    }

    static bool IsLocalOcrDependencyException(Exception ex)
    {
        var text = ex.ToString();
        return ex is DllNotFoundException
            || ex is BadImageFormatException
            || text.Contains("OpenCvSharp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("paddle_inference", StringComparison.OrdinalIgnoreCase)
            || text.Contains("pdfium", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Native OCR", StringComparison.OrdinalIgnoreCase);
    }
}
