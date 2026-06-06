using System.Diagnostics;
using System.Text.Json;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;
using SkiaSharp;
using PdfCore;

namespace Nong.Cli.Tests;

public class PdfCommandTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    (string json, int exitCode) Run(params string[] args)
    {
        var allArgs = new List<string> { NongDll };
        allArgs.AddRange(args);

        var psi = new ProcessStartInfo("dotnet", allArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit(60000);
        var json = proc.StandardOutput.ReadToEnd();
        return (json, proc.ExitCode);
    }

    JsonDocument Parse(string json) => JsonDocument.Parse(json);

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    static string CreateTextPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-pdf-text-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var page = builder.AddPage(595, 842);
        page.AddText("Stage18 PDF Title", 18, new PdfPoint(72, 760), bold);
        page.AddText("This is a deterministic text PDF for Nong PDF slicing.", 12, new PdfPoint(72, 720), font);
        page.AddText("It has selectable text, coordinates, fonts, and reading order.", 12, new PdfPoint(72, 700), font);
        page.AddText("Table A | Treatment | Yield", 12, new PdfPoint(72, 660), font);
        page.AddText("Row 1 | Control | 12.5", 12, new PdfPoint(72, 640), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    [Fact]
    public void PdfCheck_TextPdf_ReturnsClassification()
    {
        RequireCli();
        var pdf = CreateTextPdf();
        try
        {
            var (json, exit) = Run("pdf", "check", pdf, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            var root = doc.RootElement;
            Assert.Equal("ok", root.GetProperty("status").GetString());
            Assert.Equal("pdf check", root.GetProperty("command").GetString());
            Assert.Equal("text", root.GetProperty("data").GetProperty("classification").GetString());
            Assert.True(root.GetProperty("data").GetProperty("textCharCount").GetInt32() > 0);
        }
        finally { try { File.Delete(pdf); } catch { } }
    }

    [Fact]
    public void PdfDissect_TextPdf_WritesNongMarkSlice()
    {
        RequireCli();
        var pdf = CreateTextPdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("pdf", "dissect", pdf, "--output", outDir, "--mode", "auto", "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("pdf dissect", doc.RootElement.GetProperty("command").GetString());

            Assert.True(File.Exists(Path.Combine(outDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "document.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.jsonl")));
            Assert.True(File.Exists(Path.Combine(outDir, "structure.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "format.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.nongmark")));
            Assert.True(File.Exists(Path.Combine(outDir, "preview", "content.md")));
            Assert.True(File.Exists(Path.Combine(outDir, "assets", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "diagnostics", "check.json")));
            Assert.True(new FileInfo(Path.Combine(outDir, "content.nongmark")).Length > 0);

            var firstContentLine = File.ReadLines(Path.Combine(outDir, "content.jsonl"))
                .First(line => line.Contains("\"kind\":\"heading\"") || line.Contains("\"kind\":\"paragraph\""));
            using var lineDoc = Parse(firstContentLine);
            var lineRoot = lineDoc.RootElement;
            Assert.False(string.IsNullOrWhiteSpace(lineRoot.GetProperty("blockId").GetString()));
            Assert.True(lineRoot.GetProperty("page").GetInt32() >= 1);
            Assert.Equal("pdfText", lineRoot.GetProperty("source").GetString());
            Assert.True(lineRoot.GetProperty("bbox").GetArrayLength() == 4);

            var nongmark = File.ReadAllText(Path.Combine(outDir, "content.nongmark"));
            Assert.Contains("::: page", nongmark);
            Assert.Contains("bbox=", nongmark);
            Assert.Contains("source=pdfText", nongmark);
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfImages_TextPdf_WritesEmptyManifest()
    {
        RequireCli();
        var pdf = CreateTextPdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-images-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("pdf", "images", pdf, "--output", outDir, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Path.Combine(outDir, "manifest.json")));
            using var manifest = Parse(File.ReadAllText(Path.Combine(outDir, "manifest.json")));
            Assert.Equal(0, manifest.RootElement.GetProperty("items").GetArrayLength());
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfRender_TextPdf_CompositesWhiteBackground()
    {
        RequireCli();
        var pdf = CreateTextPdf();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-pdf-render-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("pdf", "render", pdf, "--output", outDir, "--dpi", "150", "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());

            var pagePath = Path.Combine(outDir, "page-0001.png");
            Assert.True(File.Exists(pagePath));
            using var bitmap = SKBitmap.Decode(pagePath);
            var background = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
            Assert.True(background.Red > 240 && background.Green > 240 && background.Blue > 240,
                $"Expected white page background, got R={background.Red} G={background.Green} B={background.Blue}");
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void PdfRenderCrop_MapsPdfBboxToRenderedPixels()
    {
        var pdf = Path.Combine(Path.GetTempPath(), "nong-pdf-crop-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        var outPath = Path.Combine(Path.GetTempPath(), "nong-pdf-crop-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        try
        {
            using var builder = new PdfDocumentBuilder();
            var page = builder.AddPage(200, 200);
            page.SetTextAndFillColor(255, 0, 0);
            page.DrawRectangle(new PdfPoint(50, 60), 40, 30, 0, true);
            File.WriteAllBytes(pdf, builder.Build());

            var crop = PdfPageRenderer.RenderCrop(pdf, 1, 200, 200, new[] { 50d, 60d, 90d, 90d }, outPath, dpi: 144, paddingPx: 0);

            Assert.Equal(80, crop.Width);
            Assert.Equal(60, crop.Height);
            Assert.True(File.Exists(outPath));
            using var bitmap = SKBitmap.Decode(outPath);
            var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
            Assert.True(center.Red > 180 && center.Green < 80 && center.Blue < 80,
                $"Expected red crop center, got R={center.Red} G={center.Green} B={center.Blue}");
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void PdfCheck_MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("pdf", "check", "nonexistent.pdf", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void PdfCheck_NonPdf_Returns_E002()
    {
        RequireCli();
        var path = Path.Combine(Path.GetTempPath(), "nong-not-pdf-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        File.WriteAllText(path, "not pdf");
        try
        {
            var (json, exit) = Run("pdf", "check", path, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("E002", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
