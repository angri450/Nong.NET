using OpenCvSharp;
using System.Runtime.InteropServices;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace MultiModalCore;

/// <summary>
/// PP-OCRv5 本地 .NET 推理客户端。
/// 使用 Sdcb.PaddleOCR + 本地 V5 中文模型 + 平台 native runtime，
/// 不依赖 Python、pip 或外部 OCR 可执行文件。
/// </summary>
public sealed class PpOcrV5Client : IDisposable
{
    private static readonly object NativeRuntimeSync = new();
    private static bool _nativeRuntimeLoaded;
    private static string? _nativeRuntimeDir;

    private readonly Lazy<PaddleOcrAll> _engine;
    private bool _disposed;

    /// <summary>已解析的模型目录路径（兼容旧 manifest/cache 查询，打包模型时可能为 null）。</summary>
    public string? ModelDir { get; }

    /// <summary>模型是否可用。</summary>
    public bool IsAvailable => CheckEnvironment().Available;

    /// <param name="modelDir">保留兼容参数。当前默认使用 NuGet 内置 LocalFullModels.ChineseV5。</param>
    public PpOcrV5Client(string? modelDir = null)
    {
        ModelDir = PpOcrV5ModelResolver.Resolve(modelDir);
        _engine = new Lazy<PaddleOcrAll>(CreateEngine);
    }

    /// <summary>
    /// 对单张图片执行 OCR 识别。
    /// </summary>
    public Task<PpOcrV5Result> RecognizeAsync(string imagePath, CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new PaddleOcrException("Image path is required.");
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image not found: {imagePath}", imagePath);

        cancel.ThrowIfCancellationRequested();

        EnsureNativeRuntimeLoaded();

        using var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (mat.Empty())
            throw new PaddleOcrException($"Cannot decode image: {imagePath}");

        var result = _engine.Value.Run(mat);
        var page = new PpOcrV5Page
        {
            Page = 1,
            Width = mat.Width,
            Height = mat.Height
        };

        var index = 0;
        foreach (var region in result.Regions)
        {
            index++;
            var points = region.Rect.Points();
            page.Blocks.Add(new PpOcrV5Block
            {
                Id = $"ocr{index:D4}",
                Text = region.Text,
                Confidence = region.Score,
                Bbox = ToAxisAlignedBbox(points),
                Polygon = points.Select(p => new[] { p.X, p.Y }).ToArray()
            });
        }

        return Task.FromResult(new PpOcrV5Result
        {
            Engine = "pp-ocrv5-dotnet-sdcb",
            ModelId = "pp-ocrv5-mobile",
            Pages = new List<PpOcrV5Page> { page }
        });
    }

    public static PpOcrV5EnvironmentStatus CheckEnvironment()
    {
        try
        {
            var nativeDir = EnsureNativeRuntimeLoaded();
            var runtimeId = PpOcrV5ModelResolver.GetNativeRuntimeId();

            _ = LocalFullModels.ChineseV5;
            _ = typeof(PaddleOcrAll).Assembly.FullName;
            _ = typeof(PaddleConfig).Assembly.FullName;

            return new PpOcrV5EnvironmentStatus(
                Available: true,
                Engine: "pp-ocrv5-dotnet-sdcb",
                ModelId: "pp-ocrv5-mobile",
                Runtime: runtimeId,
                Message: $"Pure .NET PP-OCRv5 runtime is available; no Python required. Native runtime: {nativeDir}");
        }
        catch (Exception ex)
        {
            return new PpOcrV5EnvironmentStatus(
                Available: false,
                Engine: "pp-ocrv5-dotnet-sdcb",
                ModelId: "pp-ocrv5-mobile",
                Runtime: PpOcrV5ModelResolver.GetNativeRuntimeId(),
                Message: $"Pure .NET PP-OCRv5 runtime unavailable: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_engine.IsValueCreated)
            _engine.Value.Dispose();
        _disposed = true;
    }

    private static PaddleOcrAll CreateEngine()
    {
        EnsureNativeRuntimeLoaded();
        ConfigureNativeLogEnvironment();

        return new PaddleOcrAll(LocalFullModels.ChineseV5, ConfigureFastCpuDevice)
        {
            AllowRotateDetection = true,
            Enable180Classification = false,
        };
    }

    private static string? ResolveNativeRuntimeDir()
    {
        var appNative = AppContext.BaseDirectory;
        if (PpOcrV5ModelResolver.ValidateNativeRuntimeDir(appNative))
            return appNative;

        var cacheNative = PpOcrV5ModelResolver.GetNativeRuntimeCachePath();
        if (PpOcrV5ModelResolver.ValidateNativeRuntimeDir(cacheNative))
            return cacheNative;

        return null;
    }

    private static string EnsureNativeRuntimeLoaded()
    {
        lock (NativeRuntimeSync)
        {
            if (_nativeRuntimeLoaded && _nativeRuntimeDir != null)
                return _nativeRuntimeDir;

            var nativeDir = ResolveNativeRuntimeDir();
            if (nativeDir == null)
                throw new PaddleOcrException($"Native OCR runtime not installed. Run 'nong ocr install-model pp-ocrv5-mobile --json'. Cache: {PpOcrV5ModelResolver.GetNativeRuntimeCachePath()}");

            ConfigureNativeLogEnvironment();
            AddNativeDirectoryToPath(nativeDir);
            foreach (var file in PpOcrV5ModelResolver.GetNativeRuntimeLoadFiles())
                LoadNativeLibrary(nativeDir, file);

            _nativeRuntimeDir = nativeDir;
            _nativeRuntimeLoaded = true;
            return nativeDir;
        }
    }

    private static void ConfigureNativeLogEnvironment()
    {
        Environment.SetEnvironmentVariable("GLOG_minloglevel", "3");
        Environment.SetEnvironmentVariable("FLAGS_minloglevel", "3");
        Environment.SetEnvironmentVariable("FLAGS_logtostderr", "0");
        Environment.SetEnvironmentVariable("FLAGS_alsologtostderr", "0");
        Environment.SetEnvironmentVariable("FLAGS_stderrthreshold", "3");
        Environment.SetEnvironmentVariable("DNNL_VERBOSE", "0");
        Environment.SetEnvironmentVariable("ONEDNN_VERBOSE", "0");
        Environment.SetEnvironmentVariable("MKLDNN_VERBOSE", "0");
    }

    private static void ConfigureFastCpuDevice(PaddleConfig config)
    {
        var threadCount = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
        var configure = OperatingSystem.IsWindows()
            ? PaddleDevice.OneDnn(cacheCapacity: 10, cpuMathThreadCount: threadCount, memoryOptimized: true, glogEnabled: false)
            : PaddleDevice.Blas(cpuMathThreadCount: threadCount, memoryOptimized: true, glogEnabled: false);

        configure(config);
        config.GLogEnabled = false;
        PaddleConfig.GLogMinLevel = 3;
    }

    private static void AddNativeDirectoryToPath(string nativeDir)
    {
        var current = Environment.GetEnvironmentVariable("PATH") ?? "";
        var parts = current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => string.Equals(Path.GetFullPath(p).TrimEnd('\\', '/'),
                Path.GetFullPath(nativeDir).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase)))
            return;

        Environment.SetEnvironmentVariable("PATH", nativeDir + Path.PathSeparator + current);
    }

    private static void LoadNativeLibrary(string nativeDir, string fileName)
    {
        var path = Path.Combine(nativeDir, fileName);
        if (!File.Exists(path))
            throw new PaddleOcrException($"Native OCR dependency missing: {path}");

        try
        {
            NativeLibrary.Load(path);
        }
        catch (Exception ex)
        {
            throw new PaddleOcrException($"Cannot load native OCR dependency {fileName} from {nativeDir}: {ex.Message}", ex);
        }
    }

    private static float[] ToAxisAlignedBbox(Point2f[] points)
    {
        if (points.Length == 0) return Array.Empty<float>();
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        return new[] { minX, minY, maxX, maxY };
    }
}

public sealed record PpOcrV5EnvironmentStatus(
    bool Available,
    string Engine,
    string ModelId,
    string Runtime,
    string Message);

/// <summary>PP-OCRv5 识别结果。</summary>
public sealed record PpOcrV5Result
{
    public string Engine { get; set; } = "PP-OCRv5";
    public string ModelId { get; set; } = "pp-ocrv5-mobile";
    public List<PpOcrV5Page> Pages { get; set; } = new();
}

/// <summary>单页识别结果。</summary>
public sealed record PpOcrV5Page
{
    public int Page { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<PpOcrV5Block> Blocks { get; set; } = new();
}

/// <summary>单个文字块识别结果。</summary>
public sealed record PpOcrV5Block
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public double Confidence { get; set; }
    public float[] Bbox { get; set; } = Array.Empty<float>();
    public float[][]? Polygon { get; set; }
}
