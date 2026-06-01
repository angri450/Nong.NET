# Angri450.Nong.ThirdParty

Foundation DLL for the Nong.NET toolkit. Merges source code from 15 open-source libraries into a single assembly.

## Merged Libraries

| Library | License | Purpose |
|---------|---------|---------|
| ClosedXML | MIT | Read/write Excel (.xlsx) |
| DocumentFormat.OpenXml | MIT | Low-level OpenXML operations |
| ScottPlot | MIT | Statistical chart rendering |
| MSAGL | MIT | Graph layout (Sugiyama/force-directed) |
| SkiaSharp | MIT | 2D graphics engine |
| HarfBuzzSharp | MIT | Text shaping |
| SixLabors.Fonts | Apache-2.0 | Font metrics |

## Install

```bash
dotnet add package Angri450.Nong.ThirdParty
```

## Usage

Direct access to any merged library API:

```csharp
using ClosedXML.Excel;
using ScottPlot;
using SkiaSharp;
```

## Dependencies

Managed code only. Native platform binaries (~65 MB) auto-downloaded at runtime via NuGet.
