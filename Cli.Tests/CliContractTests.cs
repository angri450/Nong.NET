using System.Diagnostics;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ClosedXML.Excel;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using PandocCore;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Nong.Cli.Tests;

public class CliContractTests
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

    static string TempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    JsonDocument Parse(string json) => JsonDocument.Parse(json);

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    // ===== Manifest tests =====

    [Fact]
    public void Commands_Json_ReturnsOnlyImplemented()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("commands", root.GetProperty("command").GetString());

        foreach (var cmd in root.GetProperty("data").EnumerateArray())
        {
            var status = cmd.GetProperty("status").GetString();
            Assert.Equal("implemented", status);
        }
    }

    [Fact]
    public void Commands_Json_UsesCanonicalWordAddNames()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var names = doc.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Contains("word add paragraph", names);
        Assert.Contains("word add table", names);
        Assert.Contains("word add math", names);
        Assert.Contains("word create", names);
        Assert.Contains("word academic-format", names);
        Assert.Contains("word format-audit", names);
        Assert.Contains("word repair-plan", names);
        Assert.DoesNotContain("word add-paragraph", names);
        Assert.DoesNotContain("word add-table", names);
        Assert.DoesNotContain("word add-math", names);
    }

    [Fact]
    public void Commands_Json_DistinguishesInternalAndVisibleWordRepair()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var commands = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        var fixOrder = commands.Single(c => c.GetProperty("name").GetString() == "word fix-order");
        var academicFormat = commands.Single(c => c.GetProperty("name").GetString() == "word academic-format");
        var formatAudit = commands.Single(c => c.GetProperty("name").GetString() == "word format-audit");
        var repairPlan = commands.Single(c => c.GetProperty("name").GetString() == "word repair-plan");

        Assert.Contains("Internal OOXML/structure repair only", fixOrder.GetProperty("description").GetString());
        Assert.Contains("Visible academic Word formatting repair", academicFormat.GetProperty("description").GetString());
        Assert.Contains("visible Word formatting evidence audit", formatAudit.GetProperty("description").GetString());
        Assert.Contains("which Word repair command", repairPlan.GetProperty("description").GetString());
    }

    [Fact]
    public void WordRepairPlan_ReturnsMachineReadableRouting()
    {
        RequireCli();
        var (json, exit) = Run("word", "repair-plan", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("word repair-plan", root.GetProperty("command").GetString());
        var data = root.GetProperty("data");
        Assert.Equal("nong-word/repair-plan/v1", data.GetProperty("schemaVersion").GetString());
        Assert.Contains(data.GetProperty("rules").EnumerateArray(),
            rule => rule.GetProperty("command").GetString() == "word academic-format"
                && rule.GetProperty("note").GetString()!.Contains("visible formatting", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(data.GetProperty("rules").EnumerateArray(),
            rule => rule.GetProperty("command").GetString() == "word format-audit"
                && rule.GetProperty("note").GetString()!.Contains("read-only", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Do not claim visible Word repair",
            data.GetProperty("forbiddenCompletionClaim").GetString());
    }

    [Fact]
    public void Commands_All_ReturnsAllCommands()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--all", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetArrayLength() >= 48, "commands --all should return 48+ entries");
        bool hasImpl = false;
        foreach (var cmd in data.EnumerateArray())
        {
            var s = cmd.GetProperty("status").GetString();
            if (s == "implemented") hasImpl = true;
        }
        Assert.True(hasImpl, "commands --all should include implemented");
    }

    [Fact]
    public void Commands_All_EveryEntryHasRequiredFields()
    {
        RequireCli();
        var (json, exit) = Run("commands", "--all", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        foreach (var cmd in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            Assert.True(cmd.TryGetProperty("name", out _));
            Assert.True(cmd.TryGetProperty("description", out _));
            Assert.True(cmd.TryGetProperty("group", out _));
            Assert.True(cmd.TryGetProperty("status", out _));
        }
    }

    // ===== Newly implemented commands: missing file → E001 =====

    [Theory]
    [InlineData("word", "stats", "nonexistent.docx")]
    [InlineData("word", "fonts", "nonexistent.docx")]
    [InlineData("word", "styles", "nonexistent.docx")]
    [InlineData("word", "dissect", "nonexistent.docx")]
    [InlineData("chart", "line", "nonexistent.json", "-o", "out.png")]
    [InlineData("chart", "scatter", "nonexistent.json", "-o", "out.png")]
    [InlineData("chart", "pie", "nonexistent.json", "-o", "out.png")]
    [InlineData("diagram", "tree", "nonexistent.json", "-o", "out.png")]
    [InlineData("excel", "dissect", "nonexistent.xlsx", "-o", "out.slice")]
    [InlineData("pptx", "read", "nonexistent.pptx")]
    [InlineData("pptx", "slides", "nonexistent.pptx")]
    [InlineData("pptx", "dissect", "nonexistent.pptx", "-o", "out.slice")]
    public void NewCommand_MissingFile_Returns_E001(params string[] args)
    {
        RequireCli();
        var allArgs = new List<string>(args) { "--json" };
        var (json, exit) = Run(allArgs.ToArray());
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void SliceInspect_MissingDirectory_Returns_E001()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-missing-slice-" + Guid.NewGuid().ToString("N")[..8]);

        var (json, exit) = Run("slice", "inspect", dir, "--json");

        Assert.NotEqual(0, exit);
        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("slice inspect", doc.RootElement.GetProperty("command").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Error path tests =====

    [Fact]
    public void MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("chart", "anova", "nonexistent_file.json", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void InvalidSpec_Returns_E006()
    {
        RequireCli();
        var specPath = TempFile("not valid json {{{");
        try
        {
            var outPath = Path.Combine(Path.GetTempPath(), "test-out.docx");
            var (json, exit) = Run("inspect", "write-paper", specPath, "-o", outPath, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally { File.Delete(specPath); }
    }

    // ===== JSON schema tests =====

    static readonly string[] RequiredFields =
        { "status", "command", "summary", "data", "issues", "artifacts", "metrics", "errors", "meta" };

    [Theory]
    [InlineData("word", "preview", "nonexistent.docx")]
    [InlineData("chart", "anova", "nonexistent.json")]
    [InlineData("chart", "duncan", "nonexistent.json")]
    public void ErrorResponse_HasAllSchemaFields(string group, string sub, string fileArg)
    {
        RequireCli();
        var (json, exit) = Run(group, sub, fileArg, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        foreach (var field in RequiredFields)
            Assert.True(root.TryGetProperty(field, out _), $"Missing field: {field}");

        Assert.True(root.TryGetProperty("meta", out var meta));
        Assert.True(meta.TryGetProperty("durationMs", out _), "Missing meta.durationMs");
        Assert.True(meta.TryGetProperty("version", out _), "Missing meta.version");
    }

    // ===== skill commands =====

    [Fact]
    public void SkillValidate_EmptyPath_Returns_E003()
    {
        RequireCli();
        var (json, exit) = Run("skill", "validate", "", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("E003", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void SkillValidate_MissingDir_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("skill", "validate", "C:\\nonexistent\\path", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void SkillScan_EmptyDir_ReturnsOk()
    {
        RequireCli();
        var emptyDir = Path.Combine(Path.GetTempPath(), "nong-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var (json, exit) = Run("skill", "scan", emptyDir, "--json");
            Assert.Equal(0, exit);
            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally { Directory.Delete(emptyDir); }
    }

    [Fact]
    public void SkillInventory_MissingDir_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("skill", "inventory", "C:\\nonexistent\\path", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void SkillPackage_InvalidDir_Returns_E006()
    {
        RequireCli();
        var emptyDir = Path.Combine(Path.GetTempPath(), "nong-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var (json, exit) = Run("skill", "package", emptyDir, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally { Directory.Delete(emptyDir); }
    }

    // ===== Excel pipeline tests =====

    [Fact]
    public void ExcelCreate_Sheets_Read_ToGroups_RoundTrip()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-excel-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var specPath = Path.Combine(dir, "spec.json");
            var xlsxPath = Path.Combine(dir, "test.xlsx");
            File.WriteAllText(specPath, @"{""sheets"":[{""name"":""Data"",""headers"":[""Treatment"",""Yield""],""rows"":[[""A"",1.2],[""A"",1.3],[""B"",2.1]]}]}");

            // Create xlsx
            var (createJson, createExit) = Run("excel", "create", specPath, "-o", xlsxPath, "--json");
            Assert.Equal(0, createExit);
            Assert.Contains("ok", createJson);

            // sheets
            var (sheetsJson, sheetsExit) = Run("excel", "sheets", xlsxPath, "--json");
            Assert.Equal(0, sheetsExit);

            // read
            var (readJson, readExit) = Run("excel", "read", xlsxPath, "--json");
            Assert.Equal(0, readExit);

            // dissect
            var sliceDir = Path.Combine(dir, "excel.slice");
            var (sliceJson, sliceExit) = Run("excel", "dissect", xlsxPath, "-o", sliceDir, "--json");
            Assert.Equal(0, sliceExit);
            using var sliceDoc = Parse(sliceJson);
            Assert.Equal("excel dissect", sliceDoc.RootElement.GetProperty("command").GetString());
            Assert.Equal("ok", sliceDoc.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Path.Combine(sliceDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "content.nongmark")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "preview", "content.txt")));
            using var manifest = Parse(File.ReadAllText(Path.Combine(sliceDir, "manifest.json")));
            Assert.Equal("nong-pandoc/package/v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("xlsx", manifest.RootElement.GetProperty("source").GetProperty("format").GetString());
            Assert.Equal("content.nongmark", manifest.RootElement.GetProperty("streams").GetProperty("contentNongMark").GetString());

            // to-groups --raw (pipeline mode)
            var (tgJson, tgExit) = Run("excel", "to-groups", xlsxPath, "--group", "A", "--value", "B", "--raw", "--json");
            Assert.Equal(0, tgExit);
            var tgDoc = Parse(tgJson);
            Assert.True(tgDoc.RootElement.TryGetProperty("A", out _));
            Assert.True(tgDoc.RootElement.TryGetProperty("B", out _));

            // to-groups --json (structured mode) with sheet name
            var (tgJson2, tgExit2) = Run("excel", "to-groups", xlsxPath, "--sheet", "Data", "--group", "A", "--value", "B", "--json");
            Assert.Equal(0, tgExit2);
            var tgDoc2 = Parse(tgJson2);
            Assert.Equal("excel to-groups", tgDoc2.RootElement.GetProperty("command").GetString());
            Assert.Equal("ok", tgDoc2.RootElement.GetProperty("status").GetString());

            // to-groups with non-existent sheet → E006
            var (tgErr, tgErrExit) = Run("excel", "to-groups", xlsxPath, "--sheet", "NoSuchSheet", "--group", "A", "--value", "B", "--json");
            Assert.NotEqual(0, tgErrExit);
            var tgErrDoc = Parse(tgErr);
            Assert.Equal("E006", tgErrDoc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ExcelDissect_IncludesFormulaMergeAndTableEvidence()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-excel-evidence-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var xlsxPath = Path.Combine(dir, "evidence.xlsx");
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Data");
                worksheet.Cell("A1").Value = "Merged Header";
                worksheet.Range("A1:B1").Merge();
                worksheet.Cell("A2").Value = "Treatment";
                worksheet.Cell("B2").Value = "Yield";
                worksheet.Cell("A3").Value = "A";
                worksheet.Cell("B3").Value = 1.2;
                worksheet.Cell("A4").Value = "B";
                worksheet.Cell("B4").Value = 2.3;
                worksheet.Cell("C2").Value = "DoubleYield";
                worksheet.Cell("C3").FormulaA1 = "B3*2";
                worksheet.Cell("C4").FormulaA1 = "B4*2";
                worksheet.Range("A2:C4").CreateTable("YieldTable");
                workbook.SaveAs(xlsxPath);
            }

            var sliceDir = Path.Combine(dir, "excel.slice");
            var (json, exit) = Run("excel", "dissect", xlsxPath, "-o", sliceDir, "--json");
            Assert.Equal(0, exit);

            using var structure = Parse(File.ReadAllText(Path.Combine(sliceDir, "structure.json")));
            var sheet = structure.RootElement.GetProperty("sheets")[0];
            Assert.Equal("A1:C4", sheet.GetProperty("usedRange").GetProperty("address").GetString());
            Assert.True(sheet.GetProperty("mergedRanges").GetArrayLength() >= 1);
            Assert.Contains(sheet.GetProperty("formulas").EnumerateArray(),
                f => f.GetProperty("formulaA1").GetString() == "B3*2");
            Assert.Contains(sheet.GetProperty("tables").EnumerateArray(),
                t => t.GetProperty("name").GetString() == "YieldTable");

            using var format = Parse(File.ReadAllText(Path.Combine(sliceDir, "format.json")));
            var formatSheet = format.RootElement.GetProperty("sheets")[0];
            Assert.True(formatSheet.GetProperty("formulaCount").GetInt32() >= 2);
            Assert.True(formatSheet.GetProperty("mergedRangeCount").GetInt32() >= 1);
            Assert.True(formatSheet.GetProperty("tableCount").GetInt32() >= 1);

            using var assets = Parse(File.ReadAllText(Path.Combine(sliceDir, "assets", "manifest.json")));
            Assert.Contains(assets.RootElement.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("kind").GetString() == "formula");
            Assert.Contains(assets.RootElement.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("kind").GetString() == "mergedRange");
            Assert.Contains(assets.RootElement.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("kind").GetString() == "tableRange");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SliceInspect_WordExcelPptxPdfPackages_KeepUnifiedContract()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-slice-contract-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var wordPath = Path.Combine(dir, "word.docx");
            CreateSimpleDocx(wordPath);
            var wordSlice = Path.Combine(dir, "word.slice");
            Assert.Equal(0, Run("word", "dissect", wordPath, "-o", wordSlice, "--json").exitCode);

            var excelPath = Path.Combine(dir, "book.xlsx");
            CreateContractXlsx(excelPath);
            var excelSlice = Path.Combine(dir, "excel.slice");
            Assert.Equal(0, Run("excel", "dissect", excelPath, "-o", excelSlice, "--json").exitCode);

            var pptxPath = Path.Combine(dir, "slides.pptx");
            CreateSimplePptx(pptxPath);
            var pptxSlice = Path.Combine(dir, "pptx.slice");
            Assert.Equal(0, Run("pptx", "dissect", pptxPath, "-o", pptxSlice, "--json").exitCode);

            var pdfPath = Path.Combine(dir, "sample.pdf");
            CreateContractPdf(pdfPath);
            var pdfSlice = Path.Combine(dir, "pdf.slice");
            Assert.Equal(0, Run("pdf", "dissect", pdfPath, "-o", pdfSlice, "--json").exitCode);

            AssertSliceInspect(wordSlice, "docx");
            AssertSliceInspect(excelSlice, "xlsx");
            AssertSliceInspect(pptxSlice, "pptx");
            AssertSliceInspect(pdfSlice, "pdf");
            AssertStrictSliceInspect(wordSlice, "docx");
            AssertStrictSliceInspect(excelSlice, "xlsx");
            AssertStrictSliceInspect(pptxSlice, "pptx");
            AssertStrictSliceInspect(pdfSlice, "pdf");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SliceInspect_StrictAndQueryCommands_UseUnifiedReader()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-slice-query-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var wordPath = Path.Combine(dir, "word.docx");
            CreateSimpleDocx(wordPath);
            var sliceDir = Path.Combine(dir, "word.slice");
            Assert.Equal(0, Run("word", "dissect", wordPath, "-o", sliceDir, "--json").exitCode);

            var (inspectJson, inspectExit) = Run("slice", "inspect", sliceDir, "--strict", "--json");
            Assert.Equal(0, inspectExit);
            using var inspect = Parse(inspectJson);
            var evidence = inspect.RootElement.GetProperty("data").GetProperty("evidence");
            Assert.True(evidence.GetProperty("valid").GetBoolean());
            Assert.True(evidence.GetProperty("checkedBlocks").GetInt32() > 0);

            var (blocksJson, blocksExit) = Run("slice", "blocks", sliceDir, "--json");
            Assert.Equal(0, blocksExit);
            using var blocks = Parse(blocksJson);
            Assert.True(blocks.RootElement.GetProperty("data").GetArrayLength() > 0);
            Assert.Equal("p0001", blocks.RootElement.GetProperty("data")[0].GetProperty("blockId").GetString());

            var (blockJson, blockExit) = Run("slice", "block", sliceDir, "p0001", "--json");
            Assert.Equal(0, blockExit);
            using var block = Parse(blockJson);
            var blockData = block.RootElement.GetProperty("data");
            Assert.Equal("p0001", blockData.GetProperty("blockId").GetString());
            Assert.True(blockData.TryGetProperty("content", out _));
            Assert.True(blockData.GetProperty("structure").TryGetProperty("provenance", out _));
            Assert.True(blockData.GetProperty("format").TryGetProperty("visualEvidence", out _));

            var (assetsJson, assetsExit) = Run("slice", "assets", sliceDir, "--json");
            Assert.Equal(0, assetsExit);
            using var assets = Parse(assetsJson);
            Assert.Equal("slice assets", assets.RootElement.GetProperty("command").GetString());
            Assert.True(assets.RootElement.GetProperty("data").ValueKind == JsonValueKind.Array);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SliceInspect_StrictRejectsMissingProvenance()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-slice-strict-bad-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var sliceDir = Path.Combine(dir, "bad.slice");
            WriteSyntheticPackage(sliceDir, "docx", "bad paragraph", includeProvenance: false);

            var (json, exit) = Run("slice", "inspect", sliceDir, "--strict", "--json");

            Assert.NotEqual(0, exit);
            using var doc = Parse(json);
            Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.Contains("provenance", doc.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SliceStructure_BlockIndexUsesUnifiedProvenanceContract()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-slice-evidence-contract-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var wordPath = Path.Combine(dir, "word.docx");
            CreateSimpleDocx(wordPath);
            var wordSlice = Path.Combine(dir, "word.slice");
            Assert.Equal(0, Run("word", "dissect", wordPath, "-o", wordSlice, "--json").exitCode);
            AssertBlockProvenance(wordSlice, "docx", p =>
            {
                Assert.True(p.TryGetProperty("position", out _));
                Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("source").GetString()));
            });

            var excelPath = Path.Combine(dir, "book.xlsx");
            CreateContractXlsx(excelPath);
            var excelSlice = Path.Combine(dir, "excel.slice");
            Assert.Equal(0, Run("excel", "dissect", excelPath, "-o", excelSlice, "--json").exitCode);
            AssertBlockProvenance(excelSlice, "xlsx", p =>
            {
                Assert.Equal("Data", p.GetProperty("sheet").GetString());
                Assert.Equal("A1", p.GetProperty("address").GetString());
            });

            var pptxPath = Path.Combine(dir, "slides.pptx");
            CreateSimplePptx(pptxPath);
            var pptxSlice = Path.Combine(dir, "pptx.slice");
            Assert.Equal(0, Run("pptx", "dissect", pptxPath, "-o", pptxSlice, "--json").exitCode);
            AssertBlockProvenance(pptxSlice, "pptx", p =>
            {
                Assert.True(p.GetProperty("slide").GetInt32() >= 1);
                Assert.True(p.TryGetProperty("layout", out var layout));
                Assert.True(layout.GetProperty("width").GetDouble() > 0);
            });

            var pdfPath = Path.Combine(dir, "sample.pdf");
            CreateContractPdf(pdfPath);
            var pdfSlice = Path.Combine(dir, "pdf.slice");
            Assert.Equal(0, Run("pdf", "dissect", pdfPath, "-o", pdfSlice, "--json").exitCode);
            AssertBlockProvenance(pdfSlice, "pdf", p =>
            {
                Assert.Equal("pdfText", p.GetProperty("source").GetString());
                Assert.Equal(1, p.GetProperty("page").GetInt32());
                Assert.Equal(4, p.GetProperty("bbox").GetArrayLength());
            });
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SliceFormats_IncludeUnifiedVisualEvidence()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-slice-visual-evidence-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var wordPath = Path.Combine(dir, "word.docx");
            CreateSimpleDocx(wordPath);
            var wordSlice = Path.Combine(dir, "word.slice");
            Assert.Equal(0, Run("word", "dissect", wordPath, "-o", wordSlice, "--json").exitCode);
            AssertVisualEvidence(wordSlice, "docx");

            var excelPath = Path.Combine(dir, "book.xlsx");
            CreateContractXlsx(excelPath);
            var excelSlice = Path.Combine(dir, "excel.slice");
            Assert.Equal(0, Run("excel", "dissect", excelPath, "-o", excelSlice, "--json").exitCode);
            AssertVisualEvidence(excelSlice, "xlsx");

            var pptxPath = Path.Combine(dir, "slides.pptx");
            CreateSimplePptx(pptxPath);
            var pptxSlice = Path.Combine(dir, "pptx.slice");
            Assert.Equal(0, Run("pptx", "dissect", pptxPath, "-o", pptxSlice, "--json").exitCode);
            AssertVisualEvidence(pptxSlice, "pptx");

            var pdfPath = Path.Combine(dir, "sample.pdf");
            CreateContractPdf(pdfPath);
            var pdfSlice = Path.Combine(dir, "pdf.slice");
            Assert.Equal(0, Run("pdf", "dissect", pdfPath, "-o", pdfSlice, "--json").exitCode);
            AssertVisualEvidence(pdfSlice, "pdf");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }


    [Fact]
    public void PptxDissect_WritesNongPandocPackage()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-pptx-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var pptxPath = Path.Combine(dir, "slides.pptx");
            CreateSimplePptx(pptxPath);

            var sliceDir = Path.Combine(dir, "pptx.slice");
            var (json, exit) = Run("pptx", "dissect", pptxPath, "-o", sliceDir, "--json");
            Assert.Equal(0, exit);
            using var doc = Parse(json);
            Assert.Equal("pptx dissect", doc.RootElement.GetProperty("command").GetString());
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Path.Combine(sliceDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "document.json")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "content.jsonl")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "content.nongmark")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "structure.json")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "format.json")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "diagnostics.json")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "assets", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(sliceDir, "preview", "content.txt")));

            using var manifest = Parse(File.ReadAllText(Path.Combine(sliceDir, "manifest.json")));
            Assert.Equal("nong-pandoc/package/v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("pptx", manifest.RootElement.GetProperty("source").GetProperty("format").GetString());
            Assert.Equal("content.nongmark", manifest.RootElement.GetProperty("streams").GetProperty("contentNongMark").GetString());

            using var structure = Parse(File.ReadAllText(Path.Combine(sliceDir, "structure.json")));
            var firstSlide = structure.RootElement.GetProperty("slides")[0];
            Assert.True(firstSlide.GetProperty("shapes").GetArrayLength() >= 1);
            var titleShape = firstSlide.GetProperty("shapes").EnumerateArray()
                .First(s => s.GetProperty("kind").GetString() is "placeholder" or "shape");
            Assert.True(titleShape.TryGetProperty("layout", out var layout));
            Assert.True(layout.GetProperty("cx").GetInt64() > 0);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    static void CreateSimplePptx(string path)
    {
        using var doc = PresentationDocument.Create(path, PresentationDocumentType.Presentation);
        var presentationPart = doc.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation
        {
            SlideIdList = new P.SlideIdList()
        };

        AddSlide(presentationPart, 256U, "Quarter Report", "Nong");
        AddSlide(presentationPart, 257U, "Findings", "Yield up", "Cost down");
        presentationPart.Presentation.Save();
    }

    static void AddSlide(PresentationPart presentationPart, uint id, string title, params string[] bodyTexts)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.Slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup()))));

        var tree = slidePart.Slide.CommonSlideData!.ShapeTree!;
        tree.Append(CreateTextShape(2U, "Title", title, isTitle: true, x: 457200, y: 274320, cx: 8229600, cy: 685800));
        for (var i = 0; i < bodyTexts.Length; i++)
            tree.Append(CreateTextShape((uint)(3 + i), $"Body {i + 1}", bodyTexts[i], isTitle: false, x: 685800, y: 1371600 + (i * 457200), cx: 7315200, cy: 342900));

        slidePart.Slide.Save();
        var relId = presentationPart.GetIdOfPart(slidePart);
        presentationPart.Presentation.SlideIdList!.Append(new P.SlideId { Id = id, RelationshipId = relId });
    }

    static P.Shape CreateTextShape(
        uint id,
        string name,
        string text,
        bool isTitle,
        long x = 0,
        long y = 0,
        long cx = 0,
        long cy = 0)
    {
        var appProps = new P.ApplicationNonVisualDrawingProperties();
        if (isTitle)
            appProps.Append(new P.PlaceholderShape { Type = P.PlaceholderValues.Title });

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                appProps),
            new P.ShapeProperties(new D.Transform2D(
                new D.Offset { X = x, Y = y },
                new D.Extents { Cx = cx, Cy = cy })),
            new P.TextBody(
                new D.BodyProperties(),
                new D.ListStyle(),
                new D.Paragraph(new D.Run(new D.Text(text)))));
    }

    static void CreateSimpleDocx(string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart!.Document = new Document(new Body(
            new Paragraph(new Run(new Text("Slice Contract Title"))),
            new Paragraph(new Run(new Text("Slice contract body.")))));
    }

    static void CreateContractXlsx(string path)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Data");
        worksheet.Cell(1, 1).Value = "Treatment";
        worksheet.Cell(1, 2).Value = "Yield";
        worksheet.Cell(2, 1).Value = "A";
        worksheet.Cell(2, 2).Value = 1.2;
        worksheet.Cell(3, 1).Value = "B";
        worksheet.Cell(3, 2).Value = 2.3;
        workbook.SaveAs(path);
    }

    static void CreateContractPdf(string path)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var page = builder.AddPage(595, 842);
        page.AddText("Nong PDF Contract Title", 18, new PdfPoint(72, 760), bold);
        page.AddText("This PDF has selectable text and stable coordinates.", 12, new PdfPoint(72, 720), font);
        page.AddText("Table A | Treatment | Yield", 12, new PdfPoint(72, 680), font);
        page.AddText("Row 1 | Control | 12.5", 12, new PdfPoint(72, 660), font);
        File.WriteAllBytes(path, builder.Build());
    }

    static void WriteSyntheticPackage(string sliceDir, string format, string text, bool includeProvenance = true)
    {
        NongPandocSlicePackageWriter.Write(new NongPandocSliceWritePayload
        {
            OutputDirectory = sliceDir,
            Manifest = new NongPandocSliceManifest
            {
                Source = new NongPandocSourceInfo { Path = $"synthetic.{format}", Format = format },
                Metrics = new NongPandocMetrics { Blocks = 1, Paragraphs = 1 },
            },
            Document = new { schemaVersion = "synthetic/v1", blocks = new[] { "p0001" } },
            ContentJsonlLines = new[] { $$"""{"id":"p0001","blockId":"p0001","kind":"paragraph","text":"{{text}}"}""" },
            NongMarkText = text + "\n",
            Structure = new
            {
                schemaVersion = "synthetic/structure/v1",
                blockIndex = new Dictionary<string, object>
                {
                    ["p0001"] = BuildSyntheticBlockEntry(format, text, includeProvenance),
                },
            },
            Format = new
            {
                schemaVersion = "synthetic/format/v1",
                fonts = Array.Empty<string>(),
                visualEvidence = new NongPandocVisualEvidence
                {
                    Format = format,
                    Source = $"synthetic.{format}",
                    Layout = { "synthetic-layout" },
                },
            },
            Diagnostics = new { schemaVersion = "synthetic/diagnostics/v1", warnings = Array.Empty<string>() },
            AssetsManifest = new { schemaVersion = "synthetic/assets/v1", items = Array.Empty<object>() },
            TextPreview = text + "\n",
        });
    }

    static object BuildSyntheticBlockEntry(string format, string text, bool includeProvenance)
    {
        if (!includeProvenance)
        {
            return new
            {
                kind = "paragraph",
                order = 0,
                textPreview = text,
            };
        }

        return new
        {
            kind = "paragraph",
            order = 0,
            textPreview = text,
            provenance = new NongPandocBlockProvenance
            {
                Format = format,
                Source = format == "pdf" ? "pdfText" : "synthetic",
                Page = format == "pdf" ? 1 : null,
                Position = 0,
                Bbox = format == "pdf" ? new[] { 72d, 96d, 240d, 120d } : null,
                Confidence = "high",
            },
        };
    }

    void AssertSliceInspect(string sliceDir, string expectedFormat)
    {
        var (json, exit) = Run("slice", "inspect", sliceDir, "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("slice inspect", root.GetProperty("command").GetString());
        var data = root.GetProperty("data");
        Assert.Equal("nong-pandoc/package/v1", data.GetProperty("schemaVersion").GetString());
        Assert.Equal(expectedFormat, data.GetProperty("source").GetProperty("format").GetString());
        Assert.Equal("content.nongmark", data.GetProperty("streams").GetProperty("contentNongMark").GetString());
        Assert.Equal("structure.json", data.GetProperty("streams").GetProperty("structure").GetString());
        Assert.Equal("format.json", data.GetProperty("streams").GetProperty("format").GetString());
        Assert.Equal("diagnostics.json", data.GetProperty("streams").GetProperty("diagnostics").GetString());
        var order = data.GetProperty("aiReadOrder").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(new[] { "content.nongmark", "structure.json", "format.json", "diagnostics.json" }, order);
        Assert.True(data.GetProperty("previewAvailable").GetBoolean());
        Assert.True(new FileInfo(Path.Combine(sliceDir, "content.nongmark")).Length > 0);
        Assert.True(new FileInfo(Path.Combine(sliceDir, "structure.json")).Length > 0);
        Assert.True(new FileInfo(Path.Combine(sliceDir, "format.json")).Length > 0);
        Assert.True(new FileInfo(Path.Combine(sliceDir, "diagnostics.json")).Length > 0);
    }

    void AssertStrictSliceInspect(string sliceDir, string expectedFormat)
    {
        var (json, exit) = Run("slice", "inspect", sliceDir, "--strict", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(expectedFormat, data.GetProperty("source").GetProperty("format").GetString());
        var evidence = data.GetProperty("evidence");
        Assert.True(evidence.GetProperty("valid").GetBoolean());
        Assert.True(evidence.GetProperty("checkedBlocks").GetInt32() > 0);
    }

    void AssertBlockProvenance(string sliceDir, string expectedFormat, Action<JsonElement> extraAssertions)
    {
        using var structure = Parse(File.ReadAllText(Path.Combine(sliceDir, "structure.json")));
        var blockIndex = structure.RootElement.GetProperty("blockIndex");
        Assert.True(blockIndex.EnumerateObject().Any());
        var entry = blockIndex.EnumerateObject().First().Value;
        Assert.True(entry.TryGetProperty("provenance", out var provenance));
        Assert.Equal(expectedFormat, provenance.GetProperty("format").GetString());
        Assert.True(provenance.TryGetProperty("position", out _));
        Assert.True(provenance.TryGetProperty("confidence", out _));
        extraAssertions(provenance);
    }

    void AssertVisualEvidence(string sliceDir, string expectedFormat)
    {
        using var format = Parse(File.ReadAllText(Path.Combine(sliceDir, "format.json")));
        Assert.True(format.RootElement.TryGetProperty("visualEvidence", out var visualEvidence));
        Assert.Equal(expectedFormat, visualEvidence.GetProperty("format").GetString());
        Assert.True(visualEvidence.TryGetProperty("layout", out _));
        Assert.True(visualEvidence.TryGetProperty("warnings", out _));
    }
}
