# Angri450.Nong.Pdf

Local PDF document slicing engine for Nong.NET.

This package provides the deterministic PDF backend used by `nong pdf`:

- `pdf check`: classify text, hybrid, or scan PDFs.
- `pdf dissect`: write a Nong PDF slice with `content.nongmark`, JSONL blocks, structure, format, diagnostics, and assets.
- `pdf render`: render pages to PNG through a local PDFium runtime.
- `pdf images`: extract embedded image evidence with page and bbox provenance, including page-crop fallback when a PDF image stream cannot be decoded directly.

The primary AI-readable output is `content.nongmark`, aligned with Nong's enhanced Pandoc/NongMark document stream. Plain Markdown previews are compatibility artifacts only.

No Python, Pandoc executable, MinerU executable, or external OCR process is required for text-layer PDF slicing.

## Install

Most users should install the CLI:

```bash
dotnet tool install --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json
```

Library consumers can reference this package directly when embedding the PDF slice engine in .NET code.

## License

Apache-2.0
