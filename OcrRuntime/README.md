# Angri450.Nong OCR Runtime Bundle

Platform-specific native runtime bundle for Nong local PP-OCRv5.

This package contains native PaddleInference and OpenCvSharp runtime files only. It does not contain Python, pip packages, an external OCR executable, or user-trained models.

Install through `nong ocr install-model pp-ocrv5-mobile --json`; do not reference this package directly from application code.

## Packages

| Package | RID | Approx nupkg size |
|---------|-----|-------------------|
| `Angri450.Nong.OcrRuntime.WinX64` | `win-x64` | 115 MB |
| `Angri450.Nong.OcrRuntime.LinuxX64` | `linux-x64` | 125 MB |
| `Angri450.Nong.OcrRuntime.LinuxArm64` | `linux-arm64` | 82 MB |
| `Angri450.Nong.OcrRuntime.OsxX64` | `osx-x64` | 82 MB |
| `Angri450.Nong.OcrRuntime.OsxArm64` | `osx-arm64` | 65 MB |

## User Install

```bash
dotnet tool install --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json
nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json
```

`install-model` selects the package for the current RID, extracts native files into the Nong runtime cache, then removes temporary downloads. Upstream Sdcb/OpenCvSharp runtime packages are not used unless `--allow-upstream-fallback` is explicitly passed.

## Maintainer Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\OcrRuntime\pack-runtimes.ps1
```

The pack script validates that each nupkg contains exactly one RID native directory, required Paddle/OpenCV files, and no Python, executable OCR wrapper, model files, or debug symbols.

Publish all five runtime packages before publishing `Angri450.Nong.Cli`, then wait for Huawei mirror sync before validating client install from the Huawei NuGet source.
