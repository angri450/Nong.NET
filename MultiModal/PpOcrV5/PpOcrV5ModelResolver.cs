using System.Runtime.InteropServices;

namespace MultiModalCore;

/// <summary>
/// PP-OCRv5 模型文件解析器。按优先级查找模型目录：
/// 1. --model-dir 显式参数（最高优先级）
/// 2. NuGet 模型资源包目录（程序集同级）
/// 3. 用户缓存目录 %LOCALAPPDATA%/Angri450.Nong/models/pp-ocrv5-mobile/
/// </summary>
public static class PpOcrV5ModelResolver
{
    /// <summary>按优先级解析模型目录路径，返回第一个包含 manifest.json 的有效目录。</summary>
    public static string? Resolve(string? modelDir = null)
    {
        // 1. 显式 --model-dir 参数
        if (modelDir != null && ValidateModelDir(modelDir))
            return modelDir;

        // 2. NuGet 资源包目录（程序集同级）
        var assemblyDir = AppContext.BaseDirectory;
        var resourcePath = Path.Combine(assemblyDir, "models", "pp-ocrv5-mobile");
        if (ValidateModelDir(resourcePath))
            return resourcePath;

        // 3. 用户缓存目录
        var cachePath = GetCachePath();
        if (ValidateModelDir(cachePath))
            return cachePath;

        return null;
    }

    /// <summary>获取平台对应的用户缓存路径。</summary>
    public static string GetCachePath()
    {
        var baseDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

        return Path.Combine(baseDir, "Angri450.Nong", "models", "pp-ocrv5-mobile");
    }

    public static string GetNativeRuntimeCachePath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("NONG_OCR_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var baseDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

        return Path.Combine(baseDir, "Angri450.Nong", "runtimes", $"pp-ocrv5-{GetNativeRuntimeId()}");
    }

    public static bool ValidateNativeRuntimeDir(string dir)
    {
        if (!Directory.Exists(dir)) return false;
        return GetRequiredNativeRuntimeFiles().All(f => File.Exists(Path.Combine(dir, f)));
    }

    public static string GetNativeRuntimeId()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows() && arch == Architecture.X64)
            return "win-x64";
        if (OperatingSystem.IsLinux() && arch == Architecture.X64)
            return "linux-x64";
        if (OperatingSystem.IsLinux() && arch == Architecture.Arm64)
            return "linux-arm64";
        if (OperatingSystem.IsMacOS() && arch == Architecture.X64)
            return "osx-x64";
        if (OperatingSystem.IsMacOS() && arch == Architecture.Arm64)
            return "osx-arm64";

        var os = OperatingSystem.IsWindows() ? "win" :
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsMacOS() ? "osx" : "unknown";
        return $"{os}-{arch.ToString().ToLowerInvariant()}";
    }

    public static string[] GetRequiredNativeRuntimeFiles()
    {
        return GetNativeRuntimeId() switch
        {
            "win-x64" => new[]
            {
                "common.dll",
                "libiomp5md.dll",
                "mkldnn.dll",
                "mklml.dll",
                "onnxruntime.dll",
                "onnxruntime_providers_shared.dll",
                "opencv_videoio_ffmpeg4110_64.dll",
                "OpenCvSharpExtern.dll",
                "paddle2onnx.dll",
                "paddle_inference_c.dll",
                "phi.dll",
            },
            "linux-x64" => new[]
            {
                "libcommon.so",
                "libonnxifi.so",
                "libonnxruntime.so",
                "libonnxruntime.so.1.11.1",
                "libOpenCvSharpExtern.so",
                "libpaddle2onnx.so",
                "libpaddle2onnx.so.1.0.0rc2",
                "libpaddle_inference_c.so",
                "libphi.so",
                "libphi_core.so",
            },
            "linux-arm64" => new[]
            {
                "libcommon.so",
                "libOpenCvSharpExtern.so",
                "libpaddle_inference_c.so",
                "libphi.so",
                "libphi_core.so",
            },
            "osx-x64" => new[]
            {
                "libcommon.dylib",
                "libonnxifi.dylib",
                "libonnxifi_dummy.dylib",
                "libonnxruntime.1.11.1.dylib",
                "libonnxruntime.dylib",
                "libOpenCvSharpExtern.dylib",
                "libpaddle2onnx.1.0.0rc2.dylib",
                "libpaddle2onnx.dylib",
                "libpaddle_inference_c.dylib",
                "libphi.dylib",
                "libphi_core.dylib",
            },
            "osx-arm64" => new[]
            {
                "libcommon.dylib",
                "libOpenCvSharpExtern.dylib",
                "libpaddle_inference_c.dylib",
                "libphi.dylib",
                "libphi_core.dylib",
            },
            _ => new[]
            {
                "__unsupported_ocr_runtime__",
            }
        };
    }

    public static string[] GetNativeRuntimeLoadFiles()
    {
        return GetNativeRuntimeId() switch
        {
            "win-x64" => new[]
            {
                "paddle_inference_c.dll",
                "mklml.dll",
                "mkldnn.dll",
                "OpenCvSharpExtern.dll",
            },
            "linux-x64" or "linux-arm64" => new[]
            {
                "libpaddle_inference_c.so",
                "libOpenCvSharpExtern.so",
            },
            "osx-x64" or "osx-arm64" => new[]
            {
                "libpaddle_inference_c.dylib",
                "libOpenCvSharpExtern.dylib",
            },
            _ => new[]
            {
                "__unsupported_ocr_runtime__",
            }
        };
    }

    /// <summary>验证目录是否存在且包含 manifest.json。</summary>
    public static bool ValidateModelDir(string dir) =>
        Directory.Exists(dir) && File.Exists(Path.Combine(dir, "manifest.json"));
}
