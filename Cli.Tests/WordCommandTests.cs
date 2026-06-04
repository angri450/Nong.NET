using System.Diagnostics;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class WordCommandTests
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
        proc.WaitForExit(30000);
        var json = proc.StandardOutput.ReadToEnd();
        return (json, proc.ExitCode);
    }

    JsonDocument Parse(string json) => JsonDocument.Parse(json);

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    // ===== Fixture helpers =====

    /// <summary>Create a minimal .docx with a single "Hello World" paragraph.</summary>
    static string CreateMinimalDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-minimal-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart.Document = new Document(new Body(
            new Paragraph(new Run(new Text("Hello World")))
        ));
        return path;
    }

    /// <summary>Create a test docx with one paragraph. Caller must delete.</summary>
    static string CreateTestDocx() => CreateMinimalDocx();

    /// <summary>Create a .docx with one Heading1 paragraph followed by one body paragraph.</summary>
    static string CreateHeadingThenBodyDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-heading-body-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart.Document = new Document(new Body(
            new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                new Run(new Text("Heading One"))),
            new Paragraph(new Run(new Text("Body One")))
        ));
        return path;
    }

    // ===== Test 1: word dissect --json basic =====

    [Fact]
    public void WordDissect_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "dissect", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word dissect", doc.RootElement.GetProperty("command").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 2: word outline --json =====

    [Fact]
    public void WordOutline_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "outline", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word outline", doc.RootElement.GetProperty("command").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 3: word images --json =====

    [Fact]
    public void WordImages_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "images", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 4: word comments --json (empty doc) =====

    [Fact]
    public void WordComments_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "comments", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 5: word revisions --json (empty doc) =====

    [Fact]
    public void WordRevisions_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "revisions", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 6: word infer-format with valid Chinese format =====

    [Fact]
    public void WordInferFormat_Valid_ReturnsOk()
    {
        RequireCli();
        var (json, exit) = Run("word", "infer-format", "黑体 四号", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    // ===== Test 7: word infer-format with empty input =====

    [Fact]
    public void WordInferFormat_Empty_Returns_E006()
    {
        RequireCli();
        var (json, exit) = Run("word", "infer-format", "", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 8: word add-paragraph with missing file =====

    [Fact]
    public void WordAddParagraph_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "add-paragraph", "nonexistent.docx",
            "--spec", "{\"text\":\"Hello\"}", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 9: word add-table with missing file =====

    [Fact]
    public void WordAddTable_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "add-table", "nonexistent.docx",
            "--spec", "{\"headers\":[\"A\"],\"rows\":[[\"1\"]]}", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 10: word add-math with missing file =====

    [Fact]
    public void WordAddMath_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "add-math", "nonexistent.docx",
            "--latex", "x^2", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 11: word fix-order with missing file =====

    [Fact]
    public void WordFixOrder_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "fix-order", "nonexistent.docx", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 12: word protect with missing file =====

    [Fact]
    public void WordProtect_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "protect", "nonexistent.docx", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 13: word embed-font with missing file =====

    [Fact]
    public void WordEmbedFont_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        // docx file is checked first, so missing docx returns E001
        // (before the missing font file is even checked)
        var (json, exit) = Run("word", "embed-font", "nonexistent.docx", "nonexistent.ttf",
            "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 14: word merge with less than 2 files =====

    [Fact]
    public void WordMerge_OneFile_ReturnsError()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "merge", "nonexistent.docx", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        var code = doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString();
        Assert.True(code is "E003" or "E001",
            $"Expected E003 (missing argument) or E001 (file not found), got {code}");
    }

    // ===== Test 15: word add-paragraph with invalid JSON spec =====

    [Fact]
    public void WordAddParagraph_InvalidSpec_Returns_E006()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add-paragraph", docx,
                "--spec", "{not valid json", "-o", outPath, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 16: canonical word add paragraph with spec file =====

    [Fact]
    public void WordAddParagraph_CanonicalSpecFile_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var specPath = Path.Combine(Path.GetTempPath(), "nong-para-spec-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            File.WriteAllText(specPath, "{\"text\":\"canonical spec file paragraph\",\"style\":\"Normal\"}");
            var (json, exit) = Run("word", "add", "paragraph", docx,
                "--spec", specPath, "-o", outPath, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word add paragraph", doc.RootElement.GetProperty("command").GetString());
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(specPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 17: canonical word add math with --after =====

    [Fact]
    public void WordAddMath_CanonicalAfter_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add", "math", docx,
                "--latex", "E=mc^2", "-o", outPath, "--after", "p0001", "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word add math", doc.RootElement.GetProperty("command").GetString());
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 18: word dissect -o (WordSlice) =====

    [Fact]
    public void WordDissect_Output_WritesSevenFiles()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("word", "dissect", docx, "-o", outDir, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word dissect", doc.RootElement.GetProperty("command").GetString());

            // Verify 7 files: manifest.json, document.json, content.md,
            // content.jsonl, structure.json, format.json, assets/manifest.json
            Assert.True(File.Exists(Path.Combine(outDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "document.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.md")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.jsonl")));
            Assert.True(File.Exists(Path.Combine(outDir, "structure.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "format.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "assets", "manifest.json")));

            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(outDir, "manifest.json")));
            Assert.Equal("nongmark/v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
            Assert.True(manifest.RootElement.TryGetProperty("sourceSha256", out _));
            Assert.True(manifest.RootElement.TryGetProperty("createdAt", out _));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    // ===== Test 19: word dissect --output alias =====

    [Fact]
    public void WordDissect_OutputLongAlias_WritesSevenFiles()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("word", "dissect", docx, "--output", outDir, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Path.Combine(outDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.jsonl")));
            Assert.True(File.Exists(Path.Combine(outDir, "assets", "manifest.json")));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    // ===== Test 20: parser errors keep JSON/E003 envelope =====

    [Fact]
    public void WordAddParagraph_MissingSpec_ReturnsJson_E003()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add", "paragraph", docx, "-o", outPath, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word add paragraph", doc.RootElement.GetProperty("command").GetString());
            Assert.Equal("E003", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 21: --after p0001 uses dissect semantic paragraph ID, not raw body index =====

    [Fact]
    public void WordAddParagraph_AfterSemanticParagraph_DoesNotTargetHeading()
    {
        RequireCli();
        var docx = CreateHeadingThenBodyDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add", "paragraph", docx,
                "--spec", "{\"text\":\"Inserted After Body\"}", "-o", outPath, "--after", "p0001", "--json");
            Assert.Equal(0, exit);

            using var jsonDoc = Parse(json);
            Assert.Equal("p0002", jsonDoc.RootElement.GetProperty("data").GetProperty("blockId").GetString());

            using var outDoc = WordprocessingDocument.Open(outPath, false);
            var texts = outDoc.MainDocumentPart!.Document.Body!.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();
            Assert.Equal(new[] { "Heading One", "Body One", "Inserted After Body" }, texts);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 22: add math result ID matches dissect equation ID =====

    [Fact]
    public void WordAddMath_DissectSeesM0001Equation()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-math-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var sliceDir = Path.Combine(Path.GetTempPath(), "nong-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("word", "add", "math", docx,
                "--latex", "E=mc^2", "-o", outPath, "--json");
            Assert.Equal(0, exit);

            using var addDoc = Parse(json);
            Assert.Equal("m0001", addDoc.RootElement.GetProperty("data").GetProperty("blockId").GetString());

            var (_, dissectExit) = Run("word", "dissect", outPath, "-o", sliceDir, "--json");
            Assert.Equal(0, dissectExit);

            var jsonl = File.ReadAllLines(Path.Combine(sliceDir, "content.jsonl"));
            Assert.Contains(jsonl, line => line.Contains("\"kind\":\"equation\"") && line.Contains("\"id\":\"m0001\""));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
            try { if (Directory.Exists(sliceDir)) Directory.Delete(sliceDir, true); } catch { }
        }
    }

    // ===== Test 23: add table emits schema-valid OOXML on a minimal valid docx =====

    [Fact]
    public void WordAddTable_MinimalDoc_ValidatesOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-table-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add", "table", docx,
                "--spec", "{\"caption\":\"Table 1\",\"headers\":[\"A\",\"B\"],\"rows\":[[\"1\",\"2\"]]}",
                "-o", outPath, "--json");
            Assert.Equal(0, exit);

            using var addDoc = Parse(json);
            Assert.Equal("t0001", addDoc.RootElement.GetProperty("data").GetProperty("blockId").GetString());

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.Equal(0, validateExit);

            using var validateDoc = Parse(validateJson);
            Assert.Equal("ok", validateDoc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }
}
