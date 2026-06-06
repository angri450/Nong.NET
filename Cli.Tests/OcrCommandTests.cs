using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Nong.Cli.Tests;

public class OcrCommandTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");
    static string MultiModalDll => Path.Combine(Path.GetDirectoryName(NongDll)!, "Angri450.Nong.MultiModal.dll");

    (string json, int exitCode) Run(params string[] args)
    {
        var allArgs = new List<string> { NongDll };
        allArgs.AddRange(args);

        var psi = new ProcessStartInfo("dotnet", allArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit(30000);
        var json = proc.StandardOutput.ReadToEnd();
        return (json, proc.ExitCode);
    }

    (string stdout, string stderr, int exitCode) RunWithStderr(params string[] args)
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
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        return (stdout, stderr, proc.ExitCode);
    }

    (string json, int exitCode) RunWithEnv(IReadOnlyDictionary<string, string> env, params string[] args)
    {
        var allArgs = new List<string> { NongDll };
        allArgs.AddRange(args);

        var psi = new ProcessStartInfo("dotnet", allArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var kvp in env)
            psi.Environment[kvp.Key] = kvp.Value;

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

    static string CreateTinyPng()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-ocr-test-image-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // ===== Test 1: check-env returns environment status =====

    [Fact]
    public void CheckEnv_ReturnsOk_WithEnvFields()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "check-env", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("ocr check-env", root.GetProperty("command").GetString());

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("imageAnalyzer", out _));
        Assert.True(data.TryGetProperty("cloudToken", out _));
        Assert.True(data.TryGetProperty("localModel", out _));
        Assert.True(data.TryGetProperty("localDotNetPpOcrV5", out var local));
        Assert.True(local.GetProperty("noPython").GetBoolean());
    }

    // ===== Test 2: analyze-image with missing file returns E001 =====

    [Fact]
    public void AnalyzeImage_MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "analyze-image", "missing.png", "-o", "out", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 3: local uses pure .NET runtime or returns dependency/runtime error =====

    [Fact]
    public void OcrLocal_UsesPureDotNetRuntime()
    {
        RequireCli();
        var image = CreateTinyPng();
        try
        {
            var (json, exit) = Run("ocr", "local", image, "--json");

            using var doc = Parse(json);
            if (exit == 0)
            {
                Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
                Assert.Equal("ocr local", doc.RootElement.GetProperty("command").GetString());
                var data = doc.RootElement.GetProperty("data");
                Assert.Equal("pp-ocrv5-dotnet-sdcb", data.GetProperty("engine").GetString());
                Assert.Equal("pp-ocrv5-mobile", data.GetProperty("modelId").GetString());
                Assert.True(data.GetProperty("runtime").TryGetProperty("inferenceMode", out _));
                Assert.False(data.GetProperty("capabilities").GetProperty("pdf").GetBoolean());
                Assert.False(data.GetProperty("capabilities").GetProperty("layoutAnalysis").GetBoolean());
                Assert.False(data.GetProperty("capabilities").GetProperty("pandocAnnotations").GetBoolean());
                Assert.DoesNotContain("NaN", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Infinity", json, StringComparison.OrdinalIgnoreCase);

                var blocks = data.GetProperty("blocks");
                if (blocks.GetArrayLength() > 0)
                {
                    Assert.True(blocks[0].TryGetProperty("confidenceValid", out _));
                    Assert.True(blocks[0].TryGetProperty("geometryValid", out _));
                    Assert.True(blocks[0].TryGetProperty("numericIssue", out _));
                }
            }
            else
            {
                Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
                var code = doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString();
                Assert.True(code is "E005" or "E004", $"Expected local OCR to fail with a dependency/runtime error, got {code}");
                Assert.DoesNotContain("Install Python", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("pip", json, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            try { File.Delete(image); } catch { }
        }
    }

    [Fact]
    public void OcrLocal_PdfInput_Returns_E002_WithCloudGuidance()
    {
        RequireCli();
        var pdf = Path.Combine(Path.GetTempPath(), "nong-ocr-local-pdf-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        File.WriteAllText(pdf, "%PDF-1.4");

        try
        {
            var (json, exit) = Run("ocr", "local", pdf, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
            var error = doc.RootElement.GetProperty("errors")[0];
            Assert.Equal("E002", error.GetProperty("code").GetString());
            Assert.Contains("ocr cloud", error.GetProperty("message").GetString());
            Assert.Contains("ocr to-word", error.GetProperty("message").GetString());
        }
        finally
        {
            try { File.Delete(pdf); } catch { }
        }
    }

    [Fact]
    public void LocalOcrConfidenceSanitizer_RejectsNonFiniteValues()
    {
        RequireCli();
        Assert.True(File.Exists(MultiModalDll), $"MultiModal assembly not found: {MultiModalDll}");

        var asm = Assembly.LoadFrom(MultiModalDll);
        var type = asm.GetType("MultiModalCore.PpOcrV5Client", throwOnError: true)!;
        var method = type.GetMethod("ToFiniteConfidence", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Null(method.Invoke(null, new object[] { double.NaN }));
        Assert.Null(method.Invoke(null, new object[] { double.PositiveInfinity }));
        Assert.Null(method.Invoke(null, new object[] { double.NegativeInfinity }));

        var finite = Assert.IsType<double>(method.Invoke(null, new object[] { 0.875d }));
        Assert.Equal(0.875d, finite);
    }

    // ===== Test 4: cloud with missing file returns E001 =====

    [Fact]
    public void OcrCloud_MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "cloud", "missing.png", "-o", "out", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 5: models returns empty array =====

    [Fact]
    public void Models_ReturnsOk_WithModelsArray()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "models", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("ocr models", root.GetProperty("command").GetString());

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("models", out var models));
        Assert.Equal(JsonValueKind.Array, models.ValueKind);
        Assert.True(models.GetArrayLength() >= 1);
        Assert.True(models[0].GetProperty("noPython").GetBoolean());
    }

    // ===== Test 6: install-model pp-ocrv5-mobile dry-run returns OK =====

    [Fact]
    public void InstallModel_PpOcrV5Mobile_DryRun_ReturnsOk()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "install-model", "pp-ocrv5-mobile", "--dry-run", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("ocr install-model", root.GetProperty("command").GetString());
        var data = root.GetProperty("data");
        Assert.True(data.GetProperty("noPython").GetBoolean());
        Assert.True(data.GetProperty("domesticNuGetSources").GetArrayLength() >= 1);
        Assert.True(data.TryGetProperty("runtimeId", out var runtimeId));
        Assert.False(string.IsNullOrWhiteSpace(runtimeId.GetString()));
        Assert.True(data.TryGetProperty("runtimePackage", out var runtimePackage));
        if (runtimePackage.ValueKind != JsonValueKind.Null)
        {
            Assert.StartsWith("Angri450.Nong.OcrRuntime.",
                runtimePackage.GetProperty("id").GetString());
        }
        Assert.True(data.TryGetProperty("fallbackPackages", out var fallbackPackages));
        Assert.Equal(JsonValueKind.Array, fallbackPackages.ValueKind);
        Assert.True(data.TryGetProperty("allowUpstreamFallback", out var allowFallback));
        Assert.False(allowFallback.GetBoolean());
        Assert.Equal("disabled", data.GetProperty("upstreamFallbackDefault").GetString());
        Assert.Contains("mirrors.huaweicloud.com", data.GetProperty("runtimeInstallCommand").GetString());
        Assert.DoesNotContain("mirrors.cloud.tencent.com", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mirrors.tuna.tsinghua.edu.cn", json, StringComparison.OrdinalIgnoreCase);
    }

    // ===== Test 7: install-model can explicitly enable upstream fallback =====

    [Fact]
    public void InstallModel_DryRun_ReportsExplicitUpstreamFallback()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "install-model", "pp-ocrv5-mobile", "--dry-run", "--allow-upstream-fallback", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("allowUpstreamFallback").GetBoolean());
        Assert.Equal("disabled", data.GetProperty("upstreamFallbackDefault").GetString());
        Assert.Contains("--allow-upstream-fallback", data.GetProperty("upstreamFallbackCommand").GetString());
    }

    // ===== Test 8: native extraction handles Windows/Linux/macOS files =====

    [Fact]
    public void NativeRuntimeExtraction_AcceptsDllSoVersionedSoAndDylib()
    {
        RequireCli();
        var tempDir = Path.Combine(Path.GetTempPath(), "nong-native-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var nupkg = Path.Combine(tempDir, "runtime.nupkg");
        var outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(outputDir);

        try
        {
            using (var archive = ZipFile.Open(nupkg, ZipArchiveMode.Create))
            {
                foreach (var name in new[]
                {
                    "runtimes/linux-x64/native/a.dll",
                    "runtimes/linux-x64/native/liba.so",
                    "runtimes/linux-x64/native/libb.so.1.2.3",
                    "runtimes/linux-x64/native/libc.dylib",
                    "runtimes/linux-x64/native/readme.txt",
                    "runtimes/osx-arm64/native/other.dylib"
                })
                {
                    var entry = archive.CreateEntry(name);
                    using var stream = entry.Open();
                    using var writer = new StreamWriter(stream);
                    writer.Write("native");
                }
            }

            var asm = Assembly.LoadFrom(NongDll);
            var type = asm.GetType("Nong.Cli.Commands.OcrCommands", throwOnError: true)!;
            var method = type.GetMethod("ExtractNativeFiles", BindingFlags.NonPublic | BindingFlags.Static)!;
            var files = (List<string>)method.Invoke(null, new object[] { nupkg, "runtimes/linux-x64/native/", outputDir })!;

            Assert.Contains("a.dll", files);
            Assert.Contains("liba.so", files);
            Assert.Contains("libb.so.1.2.3", files);
            Assert.Contains("libc.dylib", files);
            Assert.DoesNotContain("readme.txt", files);
            Assert.DoesNotContain("other.dylib", files);
            Assert.True(File.Exists(Path.Combine(outputDir, "libb.so.1.2.3")));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ===== Test 9: first-party local runtime bundle installs from directory source when present =====

    [Fact]
    public void InstallModel_LocalNupkgSource_UsesFirstPartyBundleWhenPresent()
    {
        if (!OperatingSystem.IsWindows())
            return;

        RequireCli();
        var sourceDir = Path.Combine(RepoRoot, "nupkg");
        if (!Directory.Exists(sourceDir))
            return;

        var packageExists = Directory.EnumerateFiles(sourceDir, "Angri450.Nong.OcrRuntime.*.3.2.4.nupkg").Any();
        if (!packageExists)
            return;

        var runtimeDir = Path.Combine(Path.GetTempPath(), "nong-runtime-install-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (json, exit) = RunWithEnv(
                new Dictionary<string, string> { ["NONG_OCR_RUNTIME_DIR"] = runtimeDir },
                "ocr", "install-model", "pp-ocrv5-mobile", "--source", sourceDir, "--json");

            Assert.Equal(0, exit);
            using var doc = Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var installed = data.GetProperty("installed");
            Assert.Equal("nong-bundle", installed[0].GetProperty("origin").GetString());
            Assert.StartsWith("Angri450.Nong.OcrRuntime.", installed[0].GetProperty("package").GetString());
            Assert.Equal("disabled", data.GetProperty("upstreamFallbackDefault").GetString());

            var downloads = Path.Combine(runtimeDir, "downloads");
            Assert.False(Directory.Exists(downloads));
        }
        finally
        {
            try { Directory.Delete(runtimeDir, recursive: true); } catch { }
        }
    }

    // ===== Test 10: install-model invalid-id returns E006 =====

    [Fact]
    public void InstallModel_InvalidId_Returns_E006()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "install-model", "invalid-id", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 11: to-word with missing file returns E001 =====

    [Fact]
    public void ToWord_MissingFile_Returns_E001()
    {
        RequireCli();
        var (json, exit) = Run("ocr", "to-word", "missing.png", "-o", "out.docx", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 12: Error messages do not leak token values =====

    [Fact]
    public void OcrErrors_DoNotLeakToken()
    {
        RequireCli();

        // Run several OCR error paths and verify no token-like patterns in output
        var commands = new[]
        {
            new[] { "ocr", "cloud", "missing.png", "-o", "out", "--json" },
            new[] { "ocr", "analyze-image", "missing.png", "-o", "out", "--json" },
            new[] { "ocr", "to-word", "missing.png", "-o", "out.docx", "--json" },
            new[] { "ocr", "install-model", "invalid-id", "--json" },
        };

        var tempImage = CreateTinyPng();
        try
        {
            var commandList = commands.Concat(new[] { new[] { "ocr", "local", tempImage, "--json" } });

            foreach (var args in commandList)
            {
                var (stdout, stderr, _) = RunWithStderr(args);
                var combined = stdout + stderr;

                // API tokens commonly start with "sk-" or contain "bearer"
                Assert.DoesNotContain("sk-", combined, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("bearer", combined, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            try { File.Delete(tempImage); } catch { }
        }
    }
}
