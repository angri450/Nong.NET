# Angri450.Nong.Pptx

ShapeCrawler wrapper for AI agents. Fluent slide builder with 10 built-in themes.

## Dependencies

- `ShapeCrawler 0.79.2` (NuGet) — OpenXML SDK wrapper for PPTX manipulation
- Transitive: `DocumentFormat.OpenXml 3.5.1`, `Magick.NET`, `SkiaSharp` (~15 MB runtime total)

**Note on dependency trimming:** On 2026-05-30 we attempted to strip `Magick.NET`+`SkiaSharp` from ShapeCrawler by creating a lite fork (`ShapeCrawler-lite`). This failed because ShapeCrawler's internal types (`DrawingShape`, `Position`, `ShapeId`) use C# 12 primary constructors with deep type coupling. Stub shims cannot match primary constructor signatures across the inheritance chain. The experiment cost several API call rounds and confirmed: **do not attempt to strip ShapeCrawler dependencies at the source level.** The 15 MB runtime is acceptable for agent use and the NuGet package is the correct distribution method.

## What's new in 2.0.0

- **CJK Font Injection** — `<a:ea>` elements injected via `ZipFile.Update` post-processing into every text run. ShapeCrawler's `ITextPortionFont.EastAsianName` setter does not write to XML (upstream bug).
- **Shape Borders** — Native `IShapeOutline.SetHexColor()` + `Weight` API. Works without post-processing.
- **Shape Rotation** — Rotation degrees injected via post-processing into `<a:xfrm rot="...">`. `IShape.Rotation` is get-only in ShapeCrawler.
- **RemoveAllPlaceholders** — `IShape.Remove()` physically deletes placeholder shapes instead of clearing text.
- **Text margins** — `ITextBox.LeftMargin/RightMargin/TopMargin/BottomMargin` → `<a:bodyPr>` padding.
- **Paragraph spacing** — `ISpacing.BeforeSpacing/AfterSpacing` → `<a:spcBef>/<a:spcAft>`.
- **Horizontal alignment** — `IParagraph.HorizontalAlignment` → `<a:pPr algn>`.
- **Table style options** — `ITableStyleOptions.HasHeaderRow/HasBandedRows`.    

- **Layout System (布局系统)** - 6 layout types based on "gravity field" concept:
  - `SingleFocus` - 1-2 elements, large whitespace, ceremonial feel
  - `Symmetric` - 50/50 dual columns, contrast/comparison
  - `Asymmetric` - 2/3 + 1/3 split, primary/secondary
  - `ThreeColumn` - 3 balanced columns
  - `PrimarySecondary` - 1 main area + 2-3 supporting elements
  - `HeroTop` - large top banner + small bottom cards
- **Spacing System** - unified spacing constants based on 8px grid
- **Decoration System** - page numbers, accent lines, background colors
- **Card Layouts** - `TwoColumns`, `Cards`, `BigNumber`, `Quote` helpers

## What's new in 1.0.4

- **Theme System** - 4 style helpers: `StyleTitle`, `StyleBody`, `StyleBullet`, `ApplyToMasterSlide`
- **Table Styling** - automatic header styling with white text on accent color
- **SlideHelper Enhancements** - theme-aware default fonts/colors, shape geometry mapping

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

## Layout System (布局系统)

Layouts describe **visual gravity distribution**, not pixel-perfect positions. Each layout type defines a "gravity field" that guides element placement.

### Using Layouts

```csharp
SlideBuilder.Create()
    .Theme(ThemePreset.Professional)
    .AddTitleSlide(opt => opt.Title("Single Focus Example").Subtitle("One big idea"))
    
    // Use SlideHelper for custom layouts
    .AddSlide(slide => {
        slide.SingleFocus(
            mainContent: "120%",
            subtitle: "Year-over-year growth"
        );
    })
    
    .AddSlide(slide => {
        slide.Symmetric(
            leftTitle: "Before",
            leftContent: "Manual process, 8 hours",
            rightTitle: "After", 
            rightContent: "Automated, 15 minutes"
        );
    })
    
    .AddSlide(slide => {
        slide.Asymmetric(
            mainTitle: "Core Finding",
            mainContent: "AI adoption increased productivity by 40%",
            sideTitle: "Methodology",
            sideContent: "Survey of 500 companies, 2024"
        );
    })
    
    .AddSlide(slide => {
        slide.ThreeColumn(
            col1Title: "Speed", col1Content: "2x faster",
            col2Title: "Quality", col2Content: "95% accuracy",
            col3Title: "Cost", col3Content: "50% reduction"
        );
    })
    
    .AddSlide(slide => {
        slide.PrimarySecondary(
            mainTitle: "Key Insight",
            mainContent: "The data shows clear correlation between AI adoption and revenue growth",
            supportingItems: new[] {
                ("Sample Size", "500 companies"),
                ("Time Period", "2020-2024"),
                ("Confidence", "95%")
            }
        );
    })
    
    .AddSlide(slide => {
        slide.HeroTop(
            heroTitle: "Q4 Results",
            heroContent: "Record-breaking quarter",
            bottomCards: new[] {
                ("Revenue", "$120M"),
                ("Users", "2.5M"),
                ("NPS", "72"),
                ("Retention", "94%")
            }
        );
    })
    
    .Save("layouts-demo.pptx");
```

### Card Layouts (卡片布局)

```csharp
.AddSlide(slide => {
    slide.TwoColumns(
        title: "Comparison",
        leftContent: "Traditional approach: slow, expensive",
        rightContent: "Modern approach: fast, scalable"
    );
})

.AddSlide(slide => {
    slide.Cards(
        title: "Key Metrics",
        cards: new[] {
            ("Revenue", "$120M, +30% YoY"),
            ("Users", "2.5M active, +45%"),
            ("Retention", "94%, industry-leading")
        }
    );
})

.AddSlide(slide => {
    slide.BigNumber(
        number: "120%",
        description: "Year-over-year growth in enterprise adoption",
        unit: "growth"
    );
})

.AddSlide(slide => {
    slide.Quote(
        quoteText: "The future belongs to those who prepare for it today.",
        attribution: "Malcolm X"
    );
})
```

## License

Apache-2.0. Built on [ShapeCrawler](https://github.com/ShapeCrawler/ShapeCrawler).
