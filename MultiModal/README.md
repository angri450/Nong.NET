# Angri450.Nong.MultiModal

Multi-modal document processing library. Cloud OCR (PaddleOCR-VL-1.6) and local CPU OCR (PaddleOCR). Speech-to-text and text-to-speech planned.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.MultiModal)](https://www.nuget.org/packages/Angri450.Nong.MultiModal)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.MultiModal
```

### Optional: local OCR

```bash
pip install paddlepaddle paddleocr
```

Skip if you only use the cloud API — no Python required.

## Quick Start

### Cloud API (PaddleOCR-VL-1.6)

```csharp
using MultiModalCore;

var client = new PaddleOcrVlClient();  // Token from PADDLEOCR_TOKEN env var

// File → Markdown
var mdFiles = await client.ProcessAsync("scan.pdf", "output/");

// File → Word (layout-preserving)
var docxPath = await client.ProcessToWordAsync("scan.pdf", "output/result.docx");

// URL → Markdown
await client.ProcessAsync("https://example.com/document.png", "output/");

// Raw bytes → Markdown
await client.ProcessAsync(fileBytes, "scan.png", "output/");
```

### Local CPU OCR

```csharp
var local = new LocalOcrClient(pythonExe: "python", lang: "ch");

var (ok, msg) = await local.CheckEnvironmentAsync();
if (!ok) Console.WriteLine("Install: pip install paddlepaddle paddleocr");

var blocks = await local.RecognizeAsync("crop.png");
foreach (var b in blocks)
    Console.WriteLine($"[{b.Confidence:P0}] {b.Text}");
```

### Step-by-Step Control

```csharp
var client = new PaddleOcrVlClient();
var jobId = await client.SubmitFileAsync("scan.pdf");
var resultUrl = await client.WaitForJobAsync(jobId, TimeSpan.FromSeconds(5));
var mdFiles = await client.DownloadResultsAsync(resultUrl, "output/");

// Structured data for custom processing
var ocrResult = await client.DownloadResultsStructuredAsync(resultUrl, "output/");
foreach (var page in ocrResult.Pages)
    foreach (var block in page.Blocks)
        Console.WriteLine($"[{block.Label}] {block.Content}");
```

## Options

```csharp
var options = new OcrOptions
{
    UseDocOrientationClassify = true,   // Auto-detect orientation
    UseDocUnwarping = true,             // Document unwarping
    UseChartRecognition = true,         // Chart parsing
};
await client.ProcessAsync("scan.pdf", "output/", options);
```

## Word Output Pipeline

`ProcessToWordAsync` produces layout-preserving `.docx`:

1. Cloud API returns `parsing_res_list` — each block has `block_label`, `block_content`, `block_bbox`
2. `LayoutToWordConverter` maps blocks to Docx primitives:
   - `doc_title` → Title, `paragraph_title` → Heading, `text` → Body
   - `image` → embedded image (actual download), `table` → OpenXML table
   - `vision_footnote` → Footnote
3. Multi-column pages auto-detected from `block_bbox` coordinates
4. `ElementOrder.RectifyTree()` fixes OpenXML ordering before save

## Dependencies

- `Angri450.Nong.Docx` — Word generation for `ProcessToWordAsync` output

## API Reference

### PaddleOcrVlClient (Cloud)

| Method | Description |
|--------|-------------|
| `ProcessAsync(input, outputDir)` | Submit → wait → download Markdown |
| `ProcessToWordAsync(input, docxPath)` | Submit → wait → download → Word |
| `SubmitFileAsync(path)` | Submit local file, returns jobId |
| `SubmitBytesAsync(bytes, name)` | Submit in-memory data |
| `SubmitUrlAsync(url)` | Submit remote URL |
| `WaitForJobAsync(jobId, interval)` | Poll until done, returns result URL |
| `DownloadResultsAsync(resultUrl, dir)` | Download Markdown + images |
| `DownloadResultsStructuredAsync(resultUrl, dir)` | Download and return `OcrResult` |

### LocalOcrClient (CPU)

| Method | Description |
|--------|-------------|
| `RecognizeAsync(path)` | OCR a single image |
| `RecognizeAsync(bytes)` | OCR from memory |
| `RecognizeBatchAsync(paths)` | OCR multiple images |
| `CheckEnvironmentAsync()` | Verify Python + PaddleOCR |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT
