using System.CommandLine;
using System.Text.Json;
using System.IO.Compression;
using MultiModalCore;
using Nong.Cli.Common;

namespace Nong.Cli.Commands
{

public static class OcrCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("ocr", "OCR operations");
        cmd.AddCommand(CreateCloud(jsonOpt));
        cmd.AddCommand(CreateLocal(jsonOpt));
        cmd.AddCommand(CreateToWord(jsonOpt));
        cmd.AddCommand(CreateCheckEnv(jsonOpt));
        cmd.AddCommand(CreateAnalyzeImage(jsonOpt));
        cmd.AddCommand(CreateModels(jsonOpt));
        cmd.AddCommand(CreateInstallModel(jsonOpt));
        return cmd;
    }

    // ===== ocr cloud =====

    static Command CreateCloud(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to image or PDF file");
        var outOpt = new Option<string>("-o", "Output directory") { IsRequired = true };
        var cmd = new Command("cloud", "Cloud PaddleOCR-VL-1.6 via PADDLEOCR_ACCESS_TOKEN") { fileArg, outOpt };

        cmd.SetHandler((string file, string outputDir, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                CliHelpers.WriteError("ocr cloud", ErrorCodes.MissingArgument with { Message = "File path is required." }, json);
                return;
            }
            if (!File.Exists(file))
            {
                CliHelpers.WriteError("ocr cloud", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
                return;
            }

            var token = ResolveCloudToken();
            if (token == null)
            {
                CliHelpers.WriteError("ocr cloud",
                    ErrorCodes.DependencyMissing with { Message = "PaddleOCR access token not found. Set PADDLEOCR_ACCESS_TOKEN environment variable." }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(Path.Combine(outputDir, ".keep"));

                using var client = new PaddleOcrVlClient(token);

                var (ocrResult, elapsed) = CliHelpers.Time(() =>
                {
                    // Use structured download for richer output
                    var task = ProcessStructuredAsync(client, file, outputDir);
                    task.Wait();
                    return task.Result;
                });

                if (json)
                {
                    var data = new
                    {
                        pages = ocrResult.Pages.Count,
                        blocks = ocrResult.Pages.Sum(p => p.Blocks.Count),
                        pageDetails = ocrResult.Pages.Select(p => new
                        {
                            page = p.PageNumber,
                            width = p.Width,
                            height = p.Height,
                            blockCount = p.Blocks.Count,
                            blocks = p.Blocks.Select(b => new
                            {
                                label = b.BlockLabel,
                                content = b.BlockContent.Length > 200 ? b.BlockContent[..200] : b.BlockContent,
                                bbox = b.BlockBbox
                            })
                        })
                    };
                    var output = JsonOutput.Ok("ocr cloud",
                        $"OCR complete: {ocrResult.Pages.Count} pages, {ocrResult.Pages.Sum(p => p.Blocks.Count)} blocks",
                        data);
                    output.Artifacts["dir"] = Path.GetFullPath(outputDir);
                    output.Metrics["pages"] = ocrResult.Pages.Count;
                    output.Metrics["blocks"] = ocrResult.Pages.Sum(p => p.Blocks.Count);
                    output.Meta.DurationMs = elapsed;

                    // Deprecation warning for old token env var
                    if (Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN") != null &&
                        Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN") == null)
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = "deprecated_token",
                            Severity = "Warning",
                            Message = "PADDLEOCR_TOKEN is deprecated, use PADDLEOCR_ACCESS_TOKEN instead."
                        });
                    }

                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: {ocrResult.Pages.Count} pages saved to {Path.GetFullPath(outputDir)}");
                    foreach (var p in ocrResult.Pages)
                        Console.WriteLine($"  Page {p.PageNumber}: {p.Blocks.Count} blocks");
                }
            }
            catch (AggregateException ae) when (ae.InnerException != null)
            {
                WriteCloudError(ae.InnerException, json);
            }
            catch (Exception ex)
            {
                WriteCloudError(ex, json);
            }

        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    static string? ResolveCloudToken()
    {
        var accessToken = Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(accessToken))
            return accessToken;
        return Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN");
    }

    static void WriteCloudError(Exception ex, bool json)
    {
        var msg = ex.Message;
        // Don't leak token in error messages
        if (msg.Contains("bearer ", StringComparison.OrdinalIgnoreCase))
            msg = "Authentication failed. Check your PADDLEOCR_ACCESS_TOKEN.";

        if (msg.Contains("401") || msg.Contains("403"))
        {
            CliHelpers.WriteError("ocr cloud",
                ErrorCodes.DependencyMissing with { Message = "Authentication failed. PADDLEOCR_ACCESS_TOKEN is invalid or expired." }, json);
        }
        else if (msg.Contains("429"))
        {
            var output = new JsonOutput
            {
                Status = "error",
                Command = "ocr cloud",
                Summary = "Rate limited",
                Meta = new MetaInfo { Version = CliVersion.Current }
            };
            output.Errors.Add(ErrorCodes.DependencyMissing with { Message = "PaddleOCR API rate limit reached. Retry later." });
            output.Issues.Add(new Issue { Id = "rate_limited", Severity = "Warning", Message = "HTTP 429: rate limited" });
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            Environment.ExitCode = 1;
        }
        else if (msg.Contains("503") || msg.Contains("504"))
        {
            var output = new JsonOutput
            {
                Status = "error",
                Command = "ocr cloud",
                Summary = "Service unavailable",
                Meta = new MetaInfo { Version = CliVersion.Current }
            };
            output.Errors.Add(ErrorCodes.DependencyMissing with { Message = "PaddleOCR API service unavailable. Retry later." });
            output.Issues.Add(new Issue { Id = "service_unavailable", Severity = "Warning", Message = "HTTP 503/504: service unavailable" });
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            Environment.ExitCode = 1;
        }
        else if (msg.Contains("400"))
        {
            CliHelpers.WriteError("ocr cloud",
                ErrorCodes.ValidationFailed with { Message = $"Invalid request: {msg}" }, json);
        }
        else if (ex is IOException)
        {
            CliHelpers.WriteError("ocr cloud",
                ErrorCodes.WriteFailed with { Message = $"Failed to write output: {msg}" }, json);
        }
        else
        {
            CliHelpers.WriteError("ocr cloud",
                ErrorCodes.InternalError with { Message = $"PaddleOCR API failed: {msg}" }, json);
        }
    }

    static async Task<OcrResult> ProcessStructuredAsync(PaddleOcrVlClient client, string file, string outputDir)
    {
        var kind = ClassifyInput(file);
        var jobId = kind switch
        {
            "url" => await client.SubmitUrlAsync(file),
            _ => await client.SubmitFileAsync(file)
        };
        var resultUrl = await client.WaitForJobAsync(jobId, TimeSpan.FromSeconds(5));
        return await client.DownloadResultsStructuredAsync(resultUrl, outputDir);
    }

    static string ClassifyInput(string input)
    {
        if (input.StartsWith("http://") || input.StartsWith("https://"))
            return "url";
        return "localFile";
    }

    // ===== ocr local =====

    static Command CreateLocal(Option<bool> jsonOpt)
    {
        var imageArg = new Argument<string>("image", "Path to image file");
        var cmd = new Command("local", "Local PP-OCRv5 recognition with pure .NET runtime (no Python)") { imageArg };

        cmd.SetHandler((string image, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                CliHelpers.WriteError("ocr local", ErrorCodes.MissingArgument with { Message = "Image path is required." }, json);
                return;
            }
            if (!File.Exists(image))
            {
                CliHelpers.WriteError("ocr local", ErrorCodes.FileNotFound with { Message = $"Image not found: {image}" }, json);
                return;
            }

            try
            {
                using var client = new PpOcrV5Client();
                var (result, elapsed) = CliHelpers.Time(() =>
                    client.RecognizeAsync(image).GetAwaiter().GetResult());
                var page = result.Pages.FirstOrDefault();
                var blocks = page?.Blocks ?? new List<PpOcrV5Block>();

                if (json)
                {
                    var data = new
                    {
                        engine = result.Engine,
                        modelId = result.ModelId,
                        image = Path.GetFullPath(image),
                        width = page?.Width,
                        height = page?.Height,
                        blocks = blocks.Select((b, i) => new
                        {
                            id = b.Id,
                            text = b.Text,
                            confidence = b.Confidence,
                            bbox = b.Bbox,
                            polygon = b.Polygon
                        }).ToList()
                    };
                    var output = JsonOutput.Ok("ocr local",
                        $"Local OCR complete: {blocks.Count} text block(s)", data);
                    output.Metrics["blocks"] = blocks.Count;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var b in blocks)
                        Console.WriteLine($"{b.Text}\t{b.Confidence:0.###}");
                }
            }
            catch (PaddleOcrException ex)
            {
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"Local PP-OCRv5 .NET runtime is unavailable: {ex.Message}. Update Angri450.Nong.Cli, then run 'nong ocr install-model pp-ocrv5-mobile --json'. No Python is required."
                    }, json);
            }
            catch (Exception ex) when (IsLocalOcrDependencyException(ex))
            {
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"Local PP-OCRv5 .NET runtime is unavailable: {ex.Message}. Run 'nong ocr install-model pp-ocrv5-mobile --json'. No Python is required."
                    }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.InternalError with { Message = $"Local OCR failed: {ex.Message}" }, json);
            }
        }, imageArg, jsonOpt);

        return cmd;
    }

    static bool IsLocalOcrDependencyException(Exception ex)
    {
        var text = ex.ToString();
        return ex is DllNotFoundException
            || ex is BadImageFormatException
            || text.Contains("OpenCvSharp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("paddle_inference", StringComparison.OrdinalIgnoreCase)
            || text.Contains("PaddleInference", StringComparison.OrdinalIgnoreCase)
            || text.Contains("NativeMethods", StringComparison.OrdinalIgnoreCase);
    }

    // ===== ocr to-word =====

    static Command CreateToWord(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to image or PDF file");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var pagesOpt = new Option<string>("--pages", "Page range (e.g. \"1-5,10\")");
        var cmd = new Command("to-word", "Convert image/PDF to Word document via PaddleOCR-VL-1.6") { fileArg, outOpt, pagesOpt };

        cmd.SetHandler((string file, string outputPath, string? pages, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                CliHelpers.WriteError("ocr to-word", ErrorCodes.MissingArgument with { Message = "File path is required." }, json);
                return;
            }
            if (!File.Exists(file))
            {
                CliHelpers.WriteError("ocr to-word", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
                return;
            }

            var token = ResolveCloudToken();
            if (token == null)
            {
                CliHelpers.WriteError("ocr to-word",
                    ErrorCodes.DependencyMissing with { Message = "PaddleOCR access token not found. Set PADDLEOCR_ACCESS_TOKEN environment variable." }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(Path.GetFullPath(outputPath));

                using var client = new PaddleOcrVlClient(token);

                var (docxPath, elapsed) = CliHelpers.Time(() =>
                {
                    var task = client.ProcessToWordAsync(file, Path.GetFullPath(outputPath));
                    task.Wait();
                    return task.Result;
                });

                var aerr = CliHelpers.CheckArtifact(docxPath, "DOCX");
                if (aerr != null) { CliHelpers.WriteError("ocr to-word", aerr, json); return; }

                if (json)
                {
                    var output = JsonOutput.Ok("ocr to-word",
                        $"Word document created: {docxPath}");
                    output.Artifacts["docx"] = Path.GetFullPath(docxPath);
                    output.Meta.DurationMs = elapsed;

                    if (Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN") != null &&
                        Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN") == null)
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = "deprecated_token",
                            Severity = "Warning",
                            Message = "PADDLEOCR_TOKEN is deprecated, use PADDLEOCR_ACCESS_TOKEN instead."
                        });
                    }

                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: Word document saved to {Path.GetFullPath(docxPath)}");
                }
            }
            catch (AggregateException ae) when (ae.InnerException != null)
            {
                WriteToWordError(ae.InnerException, json);
            }
            catch (Exception ex)
            {
                WriteToWordError(ex, json);
            }

        }, fileArg, outOpt, pagesOpt, jsonOpt);

        return cmd;
    }

    static void WriteToWordError(Exception ex, bool json)
    {
        var msg = ex.Message;
        if (msg.Contains("bearer ", StringComparison.OrdinalIgnoreCase))
            msg = "Authentication failed. Check your PADDLEOCR_ACCESS_TOKEN.";

        if (msg.Contains("401") || msg.Contains("403"))
        {
            CliHelpers.WriteError("ocr to-word",
                ErrorCodes.DependencyMissing with { Message = "Authentication failed. PADDLEOCR_ACCESS_TOKEN is invalid or expired." }, json);
        }
        else if (ex is IOException)
        {
            CliHelpers.WriteError("ocr to-word",
                ErrorCodes.WriteFailed with { Message = $"Failed to write output: {msg}" }, json);
        }
        else
        {
            CliHelpers.WriteError("ocr to-word",
                ErrorCodes.InternalError with { Message = $"OCR to Word failed: {msg}" }, json);
        }
    }

    // ===== ocr check-env =====

    static Command CreateCheckEnv(Option<bool> jsonOpt)
    {
        var cmd = new Command("check-env", "Check OCR environment status");

        cmd.SetHandler((bool json) =>
        {
            // ImageAnalyzer: always available (pure .NET + SkiaSharp)
            var imageAnalyzerOk = true;

            // Cloud token
            var accessToken = Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN");
            var legacyToken = Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN");
            string tokenStatus;
            if (!string.IsNullOrWhiteSpace(accessToken))
                tokenStatus = "set";
            else if (!string.IsNullOrWhiteSpace(legacyToken))
                tokenStatus = "deprecated";
            else
                tokenStatus = "missing";

            // Optional legacy model cache; current local OCR uses managed .NET V5 model metadata.
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Angri450.Nong", "models", "pp-ocrv5-mobile");
            string modelStatus;
            if (Directory.Exists(cacheDir) && File.Exists(Path.Combine(cacheDir, "manifest.json")))
                modelStatus = "present";
            else
                modelStatus = "bundled";

            var localDotNet = PpOcrV5Client.CheckEnvironment();
            var localDotNetStatus = localDotNet.Available ? "ok" : "missing";

            if (json)
            {
                var data = new
                {
                    imageAnalyzer = imageAnalyzerOk ? "ok" : "error",
                    cloudToken = tokenStatus,
                    localModel = new
                    {
                        ppOcrV5Mobile = modelStatus,
                        deployment = "managed-model-bundled-native-runtime-cache",
                        mirrorHint = "Run 'nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json' to deploy the current-platform Angri450.Nong.OcrRuntime.* native runtime bundle."
                    },
                    localDotNetPpOcrV5 = new
                    {
                        status = localDotNetStatus,
                        engine = localDotNet.Engine,
                        modelId = localDotNet.ModelId,
                        runtime = localDotNet.Runtime,
                        noPython = true,
                        message = localDotNet.Message
                    }
                };
                var output = JsonOutput.Ok("ocr check-env",
                    $"imageAnalyzer={data.imageAnalyzer}, token={tokenStatus}, localDotNet={localDotNetStatus}",
                    data);
                output.Metrics["imageAnalyzer"] = imageAnalyzerOk ? 1 : 0;
                output.Metrics["cloudToken"] = tokenStatus == "missing" ? 0 : 1;
                output.Metrics["localDotNetPpOcrV5"] = localDotNet.Available ? 1 : 0;

                if (tokenStatus == "deprecated")
                {
                    output.Issues.Add(new Issue
                    {
                        Id = "deprecated_token",
                        Severity = "Warning",
                        Message = "PADDLEOCR_TOKEN is deprecated, use PADDLEOCR_ACCESS_TOKEN instead."
                    });
                }

                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"ImageAnalyzer: {(imageAnalyzerOk ? "ok" : "error")}");
                Console.WriteLine($"Cloud token: {tokenStatus}");
                Console.WriteLine($"PP-OCRv5 model: {modelStatus}");
                Console.WriteLine($"Local .NET PP-OCRv5: {localDotNetStatus} ({localDotNet.Message})");
            }
        }, jsonOpt);

        return cmd;
    }

    // ===== ocr analyze-image =====

    static Command CreateAnalyzeImage(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("image", "Path to image file");
        var outOpt = new Option<string>("-o", "Output directory") { IsRequired = true };
        var cmd = new Command("analyze-image", "Analyze image structure (no OCR, no token required)") { fileArg, outOpt };

        cmd.SetHandler((string image, string outputDir, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                CliHelpers.WriteError("ocr analyze-image",
                    ErrorCodes.MissingArgument with { Message = "Image path is required." }, json);
                return;
            }
            if (!File.Exists(image))
            {
                CliHelpers.WriteError("ocr analyze-image",
                    ErrorCodes.FileNotFound with { Message = $"Image not found: {image}" }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(Path.Combine(outputDir, ".keep"));

                var analyzer = new ImageAnalyzer();
                var (layout, elapsed) = CliHelpers.Time(() => analyzer.Analyze(image));

                var analysisPath = Path.Combine(outputDir, "image-analysis.json");
                var mapPath = Path.Combine(outputDir, "image.map.txt");

                var analysisData = new
                {
                    width = layout.OriginalWidth,
                    height = layout.OriginalHeight,
                    sampleWidth = layout.SampleWidth,
                    sampleHeight = layout.SampleHeight,
                    whitespaceRatio = layout.WhitespaceRatio,
                    blackPixelCount = layout.BlackPixelCount,
                    graphicPixelCount = layout.GraphicPixelCount,
                    edgePixelCount = layout.EdgePixelCount,
                    contentBox = layout.ContentWidth > 0
                        ? new { x = layout.ContentMinX, y = layout.ContentMinY, width = layout.ContentWidth, height = layout.ContentHeight }
                        : null,
                    regions = layout.Regions.Select(r => new
                    {
                        x = r.X, y = r.Y, width = r.Width, height = r.Height,
                        type = r.Type.ToString(), pixelCount = r.PixelCount
                    })
                };
                File.WriteAllText(analysisPath, JsonSerializer.Serialize(analysisData, CliHelpers.JsonOpts));
                File.WriteAllText(mapPath, layout.AsciiMap);

                var aerr = CliHelpers.CheckArtifact(analysisPath, "JSON");
                if (aerr != null) { CliHelpers.WriteError("ocr analyze-image", aerr, json); return; }

                if (json)
                {
                    var output = JsonOutput.Ok("ocr analyze-image",
                        $"Analyzed: {layout.OriginalWidth}x{layout.OriginalHeight}, whitespace={layout.WhitespaceRatio:P0}",
                        analysisData);
                    output.Artifacts["analysisJson"] = Path.GetFullPath(analysisPath);
                    output.Artifacts["asciiMap"] = Path.GetFullPath(mapPath);
                    output.Metrics["whitespaceRatio"] = layout.WhitespaceRatio;
                    output.Metrics["regionCount"] = layout.Regions.Count;
                    output.Meta.DurationMs = elapsed;

                    if (layout.WhitespaceRatio > 0.95)
                        output.Issues.Add(new Issue { Id = "mostly_blank", Severity = "Info", Message = "Image is mostly blank." });

                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"{layout.OriginalWidth}x{layout.OriginalHeight}, whitespace={layout.WhitespaceRatio:P0}, {layout.Regions.Count} regions");
                    Console.WriteLine($"Analysis: {analysisPath}");
                    Console.WriteLine($"Map: {mapPath}");
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex.Message.Contains("decode"))
            {
                CliHelpers.WriteError("ocr analyze-image",
                    ErrorCodes.ReadFailed with { Message = $"Cannot decode image: {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr analyze-image",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== ocr models =====

    static Command CreateModels(Option<bool> jsonOpt)
    {
        var cmd = new Command("models", "List available local OCR models");

        cmd.SetHandler((bool json) =>
        {
            var models = new List<object>();
            var cachePath = PpOcrV5ModelResolver.GetCachePath();
            var env = PpOcrV5Client.CheckEnvironment();

            models.Add(new
            {
                id = "pp-ocrv5-mobile",
                engine = env.Engine,
                runtime = env.Runtime,
                deployment = "managed-model-bundled-native-runtime-cache",
                language = "chinese-v5",
                available = env.Available,
                noPython = true,
                domesticMirror = "Run nong ocr install-model pp-ocrv5-mobile with Huawei Cloud NuGet v3 source for first-party native runtime deployment.",
                message = env.Message
            });

            if (Directory.Exists(cachePath))
            {
                string? version = null;
                string? checksum = null;

                var manifestPath = Path.Combine(cachePath, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifestJson = File.ReadAllText(manifestPath);
                        using var doc = JsonDocument.Parse(manifestJson);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("version", out var verProp))
                            version = verProp.GetString();
                    }
                    catch { /* ignore malformed manifest */ }
                }

                var checksumFile = Path.Combine(cachePath, "checksums.sha256");
                if (File.Exists(checksumFile))
                {
                    try
                    {
                        checksum = File.ReadAllText(checksumFile).Trim();
                    }
                    catch { /* ignore missing/unreadable checksum */ }
                }

                models.Add(new
                {
                    id = "pp-ocrv5-mobile",
                    engine = "legacy-cache",
                    version = version ?? "unknown",
                    path = cachePath,
                    checksum = checksum ?? "unknown"
                });
            }

            if (json)
            {
                var data = new { models };
                var output = JsonOutput.Ok("ocr models",
                    $"Found {models.Count} model(s)", data);
                output.Metrics["modelCount"] = models.Count;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                if (models.Count == 0)
                {
                    Console.WriteLine("No PP-OCRv5 models installed.");
                }
                else
                {
                    foreach (dynamic m in models)
                        Console.WriteLine(JsonSerializer.Serialize(m, CliHelpers.JsonOpts));
                }
            }
        }, jsonOpt);

        return cmd;
    }

    // ===== ocr install-model =====

    static Command CreateInstallModel(Option<bool> jsonOpt)
    {
        var modelIdArg = new Argument<string>("model-id", "Model ID to install (e.g. pp-ocrv5-mobile)");
        var dryRunOpt = new Option<bool>("--dry-run", () => false, "Report the .NET native runtime deployment plan without changing the machine");
        var sourceOpt = new Option<string>("--source",
            () => "https://mirrors.huaweicloud.com/repository/nuget/v3/index.json",
            "NuGet v3 source for native runtime packages; use a domestic mirror for client deployment");
        var allowUpstreamFallbackOpt = new Option<bool>("--allow-upstream-fallback", () => false,
            "Allow fallback to upstream Sdcb/OpenCvSharp native runtime packages when the first-party Nong runtime bundle is unavailable");
        var cmd = new Command("install-model", "Install/check pure .NET PP-OCRv5 native runtime")
        {
            modelIdArg,
            dryRunOpt,
            sourceOpt,
            allowUpstreamFallbackOpt
        };

        cmd.SetHandler((string modelId, bool dryRun, string source, bool allowUpstreamFallback, bool json) =>
        {
            if (modelId != "pp-ocrv5-mobile")
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.ValidationFailed with { Message = $"Unknown model ID: {modelId}. Supported: pp-ocrv5-mobile" }, json);
                return;
            }

            var cachePath = PpOcrV5ModelResolver.GetCachePath();
            var env = PpOcrV5Client.CheckEnvironment();
            var runtimeCache = PpOcrV5ModelResolver.GetNativeRuntimeCachePath();
            var runtimePlan = GetNativeRuntimePlan();
            var domesticNuGetSources = new[]
            {
                "https://mirrors.huaweicloud.com/repository/nuget/v3/index.json"
            };
            var installCommand = "dotnet tool install --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json";
            var runtimeInstallCommand = "nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json";
            var upstreamFallbackCommand = runtimeInstallCommand + " --allow-upstream-fallback";

            if (dryRun)
            {
                var data = new
                {
                    modelId,
                    engine = "pp-ocrv5-dotnet-sdcb",
                    deployment = "managed-model-bundled-native-runtime-cache",
                    installCommand,
                    runtimeInstallCommand,
                    domesticNuGetSources,
                    cachePath,
                    runtimeCache,
                    runtimeId = runtimePlan.RuntimeId,
                    runtimePackage = runtimePlan.BundlePackage,
                    fallbackPackages = runtimePlan.FallbackPackages,
                    allowUpstreamFallback,
                    upstreamFallbackDefault = "disabled",
                    upstreamFallbackCommand,
                    source,
                    noPython = true,
                    note = "Local OCR uses managed .NET model metadata plus first-party Nong NuGet native runtime bundles. Client machines do not need Python, pip, local model builds, or an external OCR executable. Upstream runtime packages are used only when --allow-upstream-fallback is explicitly set."
                };
                var output = JsonOutput.Ok("ocr install-model", "Dry run: pure .NET PP-OCRv5 deployment plan", data);
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                return;
            }

            if (env.Available)
            {
                var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
                var output = JsonOutput.Ok("ocr install-model",
                    "Pure .NET PP-OCRv5 is available",
                    new
                    {
                        modelId,
                        engine = env.Engine,
                        runtime = env.Runtime,
                        deployment = "managed-model-bundled-native-runtime-cache",
                        domesticNuGetSources,
                        allowUpstreamFallback,
                        upstreamFallbackDefault = "disabled",
                        cachePath,
                        runtimeCache,
                        downloadCleanup,
                        noPython = true,
                        message = env.Message
                    });
                AddCleanupWarning(output, downloadCleanup);
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                return;
            }

            if (runtimePlan.BundlePackage == null && runtimePlan.FallbackPackages.Count == 0)
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"Local PP-OCRv5 runtime installer has no supported runtime package for this platform: {runtimePlan.RuntimeId}. Supported first-party packages are WinX64, LinuxX64, LinuxArm64, OsxX64, and OsxArm64."
                    }, json);
                return;
            }

            try
            {
                var (installed, elapsed) = CliHelpers.Time(() =>
                    InstallNativeRuntime(runtimePlan, runtimeCache, source, allowUpstreamFallback));

                var after = PpOcrV5Client.CheckEnvironment();
                if (!after.Available)
                {
                    CliHelpers.WriteError("ocr install-model",
                        ErrorCodes.DependencyMissing with
                        {
                            Message = $"Native runtime files were installed, but local PP-OCRv5 is still unavailable: {after.Message}"
                        }, json);
                    return;
                }

                var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
                var output = JsonOutput.Ok("ocr install-model",
                    "Pure .NET PP-OCRv5 native runtime installed",
                    new
                    {
                        modelId,
                        engine = after.Engine,
                        runtime = after.Runtime,
                        runtimeId = runtimePlan.RuntimeId,
                        source,
                        runtimeCache,
                        installed,
                        downloadCleanup,
                        allowUpstreamFallback,
                        upstreamFallbackDefault = "disabled",
                        noPython = true,
                        message = after.Message
                    });
                AddCleanupWarning(output, downloadCleanup);
                output.Artifacts["runtimeDir"] = runtimeCache;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
                var cleanupMessage = downloadCleanup.Warning == null
                    ? ""
                    : $" Cleanup warning: {downloadCleanup.Warning}";
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"Failed to install pure .NET PP-OCRv5 native runtime from NuGet source '{source}': {ex.Message}{cleanupMessage}"
                    }, json);
            }
        }, modelIdArg, dryRunOpt, sourceOpt, allowUpstreamFallbackOpt, jsonOpt);

        return cmd;
    }

    // ===== helpers =====

    static string GetPpOcrV5ModelCachePath() => PpOcrV5ModelResolver.GetCachePath();

    sealed record NativeRuntimePackage(string Id, string Version, string NativePrefix);
    sealed record NativeRuntimePlan(string RuntimeId, NativeRuntimePackage? BundlePackage, IReadOnlyList<NativeRuntimePackage> FallbackPackages);
    sealed record RuntimeDownloadCleanup(bool Attempted, bool Cleaned, string Path, string? Warning);

    static NativeRuntimePlan GetNativeRuntimePlan()
    {
        var runtimeId = PpOcrV5ModelResolver.GetNativeRuntimeId();
        return runtimeId switch
        {
            "win-x64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.WinX64", CliVersion.Current, "runtimes/win-x64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.win64.mkl", "3.3.1.70", "runtimes/win-x64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.win", "4.11.0.20250507", "runtimes/win-x64/native/")
                }),
            "linux-x64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.LinuxX64", CliVersion.Current, "runtimes/linux-x64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.linux-x64.openblas", "3.3.1.70", "runtimes/linux-x64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.ubuntu.18.04-x64", "4.6.0.20220608", "runtimes/ubuntu.18.04-x64/native/")
                }),
            "linux-arm64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.LinuxArm64", CliVersion.Current, "runtimes/linux-arm64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.linux-arm64", "3.3.1.70", "runtimes/linux-arm64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.linux-arm64", "4.13.0.20260602", "runtimes/linux-arm64/native/")
                }),
            "osx-x64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.OsxX64", CliVersion.Current, "runtimes/osx-x64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.osx-x64", "3.3.1.70", "runtimes/osx-x64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.osx.10.15-universal", "4.7.0.20230224", "runtimes/osx-x64/native/")
                }),
            "osx-arm64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.OsxArm64", CliVersion.Current, "runtimes/osx-arm64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.osx-arm64", "3.3.1.70", "runtimes/osx-arm64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.osx.10.15-universal", "4.7.0.20230224", "runtimes/osx-arm64/native/")
                }),
            _ => new NativeRuntimePlan(runtimeId, null, Array.Empty<NativeRuntimePackage>())
        };
    }

    static List<object> InstallNativeRuntime(
        NativeRuntimePlan plan,
        string runtimeCache,
        string source,
        bool allowUpstreamFallback)
    {
        Directory.CreateDirectory(runtimeCache);
        Exception? bundleError = null;
        if (plan.BundlePackage != null)
        {
            try
            {
                var bundleInstalled = InstallNativeRuntimePackage(plan.BundlePackage, runtimeCache, source, "nong-bundle");
                WriteNativeRuntimeManifest(runtimeCache, plan, new[] { bundleInstalled });
                return new List<object> { bundleInstalled };
            }
            catch (Exception ex)
            {
                bundleError = ex;
                if (!allowUpstreamFallback)
                {
                    throw new InvalidOperationException(
                        $"First-party Nong OCR runtime bundle {plan.BundlePackage.Id} {plan.BundlePackage.Version} is unavailable or invalid. Publish/sync Angri450.Nong.OcrRuntime.* to the NuGet source, or rerun with --allow-upstream-fallback to use upstream Sdcb/OpenCvSharp packages. Bundle error: {ex.Message}",
                        ex);
                }
            }
        }

        if (!allowUpstreamFallback)
            throw new InvalidOperationException($"No first-party Nong OCR runtime bundle is configured for {plan.RuntimeId}.");

        var installed = new List<object>();
        foreach (var package in plan.FallbackPackages)
            installed.Add(InstallNativeRuntimePackage(package, runtimeCache, source, "upstream-fallback"));

        if (installed.Count == 0)
            throw new InvalidOperationException($"No native runtime package is configured for {plan.RuntimeId}.{(bundleError == null ? "" : " Bundle error: " + bundleError.Message)}");

        WriteNativeRuntimeManifest(runtimeCache, plan, installed);
        return installed;
    }

    static object InstallNativeRuntimePackage(NativeRuntimePackage package, string runtimeCache, string source, string origin)
    {
        var nupkgPath = ResolvePackageFromDirectory(package.Id, package.Version, source)
            ?? ResolvePackageFromCache(package.Id, package.Version)
            ?? DownloadNuGetPackage(package.Id, package.Version, source, runtimeCache);
        var files = ExtractNativeFiles(nupkgPath, package.NativePrefix, runtimeCache);
        return new
        {
            origin,
            package = package.Id,
            version = package.Version,
            nupkg = nupkgPath,
            files
        };
    }

    static void WriteNativeRuntimeManifest(
        string runtimeCache,
        NativeRuntimePlan plan,
        IEnumerable<object> installed)
    {
        var manifestPath = Path.Combine(runtimeCache, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
            {
            schemaVersion = "nong-ocr-native-runtime/v1",
            runtimeId = plan.RuntimeId,
            installedAt = DateTimeOffset.UtcNow,
            bundlePackage = plan.BundlePackage,
            fallbackPackages = plan.FallbackPackages,
            installed,
            noPython = true
        }, CliHelpers.JsonOpts));
    }

    static RuntimeDownloadCleanup CleanupRuntimeDownloads(string runtimeCache)
    {
        var downloads = Path.Combine(runtimeCache, "downloads");
        var downloadsFull = Path.GetFullPath(downloads);

        if (!Directory.Exists(downloadsFull))
            return new RuntimeDownloadCleanup(false, false, downloadsFull, null);

        try
        {
            var runtimeRoot = EnsureTrailingSeparator(Path.GetFullPath(runtimeCache));
            var downloadsRoot = EnsureTrailingSeparator(downloadsFull);
            if (!downloadsRoot.StartsWith(runtimeRoot, StringComparison.OrdinalIgnoreCase))
            {
                return new RuntimeDownloadCleanup(
                    true,
                    false,
                    downloadsFull,
                    $"Refused to clean download cache outside runtime cache: {downloadsFull}");
            }

            Directory.Delete(downloadsFull, recursive: true);
            return new RuntimeDownloadCleanup(true, true, downloadsFull, null);
        }
        catch (Exception ex)
        {
            return new RuntimeDownloadCleanup(
                true,
                false,
                downloadsFull,
                $"Native runtime download cache cleanup failed: {ex.Message}");
        }
    }

    static void AddCleanupWarning(JsonOutput output, RuntimeDownloadCleanup cleanup)
    {
        if (cleanup.Warning == null)
            return;

        output.Issues.Add(new Issue
        {
            Id = "runtime_download_cleanup_failed",
            Severity = "Warning",
            Message = cleanup.Warning
        });
    }

    static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            return path;
        return path + Path.DirectorySeparatorChar;
    }

    static string? ResolvePackageFromCache(string id, string version)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home)) return null;
        var lowerId = id.ToLowerInvariant();
        var path = Path.Combine(home, ".nuget", "packages", lowerId, version, $"{lowerId}.{version}.nupkg");
        return File.Exists(path) ? path : null;
    }

    static string DownloadNuGetPackage(string id, string version, string source, string runtimeCache)
    {
        var localPackage = ResolvePackageFromDirectory(id, version, source);
        if (localPackage != null)
            return localPackage;

        using var http = new HttpClient();
        using var indexDoc = JsonDocument.Parse(http.GetStringAsync(source).GetAwaiter().GetResult());
        var packageBase = indexDoc.RootElement.GetProperty("resources")
            .EnumerateArray()
            .Where(r => r.TryGetProperty("@type", out var t) &&
                        t.GetString()?.Contains("PackageBaseAddress", StringComparison.OrdinalIgnoreCase) == true)
            .Select(r => r.GetProperty("@id").GetString())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
            ?? throw new InvalidOperationException($"NuGet source has no PackageBaseAddress resource: {source}");

        var lowerId = id.ToLowerInvariant();
        var url = $"{packageBase.TrimEnd('/')}/{lowerId}/{version}/{lowerId}.{version}.nupkg";
        var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        var downloadDir = Path.Combine(runtimeCache, "downloads");
        Directory.CreateDirectory(downloadDir);
        var outPath = Path.Combine(downloadDir, $"{lowerId}.{version}.nupkg");
        File.WriteAllBytes(outPath, bytes);
        return outPath;
    }

    static string? ResolvePackageFromDirectory(string id, string version, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        string? path = null;
        if (Directory.Exists(source))
            path = source;
        else if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(source);
                if (uri.IsFile && Directory.Exists(uri.LocalPath))
                    path = uri.LocalPath;
            }
            catch
            {
                return null;
            }
        }

        if (path == null)
            return null;

        var lowerId = id.ToLowerInvariant();
        var candidates = new[]
        {
            Path.Combine(path, $"{id}.{version}.nupkg"),
            Path.Combine(path, $"{lowerId}.{version}.nupkg"),
            Path.Combine(path, lowerId, version, $"{lowerId}.{version}.nupkg")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    static List<string> ExtractNativeFiles(string nupkgPath, string nativePrefix, string runtimeCache)
    {
        var files = new List<string>();
        using var archive = ZipFile.OpenRead(nupkgPath);
        foreach (var entry in archive.Entries)
        {
            var fullName = entry.FullName.Replace('\\', '/');
            if (!fullName.StartsWith(nativePrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsNativeRuntimeFile(fullName))
                continue;

            var fileName = Path.GetFileName(fullName);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var outPath = Path.Combine(runtimeCache, fileName);
            entry.ExtractToFile(outPath, overwrite: true);
            files.Add(fileName);
        }

        if (files.Count == 0)
            throw new InvalidOperationException($"No native runtime files found under {nativePrefix} in {nupkgPath}");

        return files;
    }

    static bool IsNativeRuntimeFile(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var lower = fileName.ToLowerInvariant();
        return lower.EndsWith(".dll")
            || lower.EndsWith(".dylib")
            || lower.EndsWith(".so")
            || lower.Contains(".so.");
    }
    }
}
