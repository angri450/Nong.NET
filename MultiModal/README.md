# Angri450.Nong.MultiModal

Multi-modal document processing library. OCR first, speech-to-text and text-to-speech coming. Pure .NET, minimal dependencies.

## Install

```powershell
dotnet add package Angri450.Nong.MultiModal
```

### Optional: local OCR

```powershell
pip install paddlepaddle paddleocr
```

Skip this if you only use the cloud API.

## Quick Start

### Cloud API (PaddleOCR-VL-1.6)

```csharp
using MultiModalCore;

// Token read from PADDLEOCR_TOKEN environment variable
var client = new PaddleOcrVlClient();

// Process a local file → Markdown
var mdFiles = await client.ProcessAsync("scan.pdf", "output/");
// → output/doc_0.md, output/doc_1.md, ...

// Process a URL
await client.ProcessAsync("https://example.com/contract.png", "output/");

// Process raw bytes
byte[] fileBytes = ...;
await client.ProcessAsync(fileBytes, "scan.png", "output/");

// Process → Word (layout-preserving, depends on Angri450.Nong.Docx)
var docxPath = await client.ProcessToWordAsync("scan.pdf", "output/result.docx");
```

### Local OCR

```csharp
var local = new LocalOcrClient(pythonExe: "python", lang: "ch");

// Check environment
var (ok, msg) = await local.CheckEnvironmentAsync();
if (!ok) Console.WriteLine("Install: pip install paddlepaddle paddleocr");

// Recognize a single image
var blocks = await local.RecognizeAsync("crop.png");
foreach (var b in blocks)
    Console.WriteLine($"[{b.Confidence:P0}] {b.Text}");
```

### Step-by-step control

```csharp
var client = new PaddleOcrVlClient();
var jobId = await client.SubmitFileAsync("scan.pdf");
var resultUrl = await client.WaitForJobAsync(jobId, TimeSpan.FromSeconds(5));
var mdFiles = await client.DownloadResultsAsync(resultUrl, "output/");

// Or get structured data for custom processing
var ocrResult = await client.DownloadResultsStructuredAsync(resultUrl, "output/");
```

## API Reference

### PaddleOcrVlClient (cloud)

| Method | Description |
|---|---|
| `ProcessAsync(input, outputDir)` | One-shot: submit → wait → download Markdown |
| `ProcessToWordAsync(input, docxPath)` | One-shot: submit → wait → download → Word |
| `SubmitFileAsync(path)` | Submit local file, returns jobId |
| `SubmitBytesAsync(bytes, name)` | Submit in-memory data, returns jobId |
| `SubmitUrlAsync(url)` | Submit remote URL, returns jobId |
| `WaitForJobAsync(jobId, interval)` | Poll until done, returns result URL |
| `DownloadResultsAsync(resultUrl, dir)` | Download and save Markdown + images |
| `DownloadResultsStructuredAsync(resultUrl, dir)` | Download and return `OcrResult` |


### LocalOcrClient (local CPU)

| Method | Description |
|---|---|
| `RecognizeAsync(imagePath)` | OCR a single image |
| `RecognizeAsync(imageBytes)` | OCR from memory |
| `RecognizeBatchAsync(paths)` | OCR multiple images |
| `CheckEnvironmentAsync()` | Verify Python + PaddleOCR installation |

## Options

```csharp
var options = new OcrOptions
{
    UseDocOrientationClassify = true,   // orientation detection
    UseDocUnwarping = true,             // document unwarping
    UseChartRecognition = true,         // chart parsing
};
await client.ProcessAsync("scan.pdf", "output/", options);
```

## Word Output Pipeline

`ProcessToWordAsync` produces layout-preserving `.docx` files:

1. Cloud API returns `prunedResult.parsing_res_list` — each block has `block_label`, `block_content`, and `block_bbox`
2. `LayoutToWordConverter` maps blocks to `Angri450.Nong.Docx` primitives:
   - `doc_title` → `DocumentWriter.Title()`
   - `paragraph_title` → `DocumentWriter.Heading(2)`
   - `text` → `DocumentWriter.Body()`
   - `image` → `ImageEmbedder.EmbedSingleImage()` (actual download + embed)
   - `table` → `DocumentWriter.Table()` (HTML → OpenXML)
   - `vision_footnote` → `DocumentWriter.Footnote()`
3. Multi-column pages auto-detected from `block_bbox` coordinates, rendered with borderless tables
4. `ElementOrder.RectifyTree()` fixes OpenXML element ordering before save

## Dependency Chain

```
Angri450.Nong.MultiModal
└── Angri450.Nong.Docx
    └── DocumentFormat.OpenXml
```

No `System.Drawing.Common`, no `SixLabors.ImageSharp`. Image dimensions are read via `ImageHeaderReader.cs` — 120 lines of pure C# binary header parsing for PNG/JPEG/GIF/BMP/TIFF.

## Roadmap

- Hybrid mode: cloud layout analysis + local CPU OCR → save quota, increase speed
- ONNX Runtime migration for local OCR (remove Python dependency)
- Speech-to-text (STT)
- Text-to-speech (TTS)

## License

Apache-2.0
