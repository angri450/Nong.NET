# Angri450.Nong.Pptx

ShapeCrawler wrapper for AI agents. Fluent slide builder with 10 built-in themes.

## What's new in 1.0.2

- **ShapeMap** — `SlidePreview.ShapeMap(path)` returns structured JSON of all shapes across all slides. AI agents can browse shapes before editing, no raw XML needed.
- **RawAccessor** — L3 fallback for raw OOXML access when SlideBuilder cannot express what you need. Read/modify any part, save as new file.
- **4 new JSON themes** — midnight-executive, coral-energy, teal-trust, cherry-bold (10 total).

## Quick Start

```powershell
dotnet add package Angri450.Nong.Pptx
```

```csharp
using PptxCore;

// Create
SlideBuilder.Create()
    .Theme(ThemePreset.Professional)
    .AddTitleSlide(opt => opt.Title("Report").Subtitle("Q1").Author("Alice"))
    .AddContentSlide(opt => opt.Title("Key Results").Bullets("Revenue +30%", "Users doubled"))
    .AddTableSlide(opt => opt.Title("KPIs").Data(new[] {
        new[] { "Metric", "Target", "Actual" },
        new[] { "Revenue", "100M", "120M" }
    }))
    .AddChartSlide(opt => opt.Title("Trend").ChartTitle("Revenue").BarChart(
        new Dictionary<string, double> { { "Q1", 10 }, { "Q2", 12 }, { "Q3", 14 } }))
    .Save("output.pptx");

// Inspect
var map = SlidePreview.ShapeMap("output.pptx");
Console.WriteLine(map.Json);

// Validate
SlideValidator.ValidateAndReport("output.pptx");

// L3 fallback
var raw = new RawAccessor("output.pptx");
string xml = raw.GetPart("/ppt/slides/slide1.xml");
raw.SaveAs("repaired.pptx");
```

## Themes (10)

**Built-in (6)**: Professional, Academic, Modern, Minimal, Warm, Cool

**JSON (4 new)**: midnight-executive, coral-energy, teal-trust, cherry-bold

```csharp
var theme = ThemePreset.BuildFromJson("formats/cherry-bold.json");
SlideBuilder.Create().Theme(theme)...
```

## License

Apache-2.0. Built on [ShapeCrawler](https://github.com/ShapeCrawler/ShapeCrawler).
