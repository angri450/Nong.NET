# Angri450.Nong.Docx v2.0

OpenXML Word generation library — pure .NET, zero COM, single dependency. One-stop paper writing: generate, fill, diagnose.

## Dependency

```
Angri450.Nong.Docx
└── DocumentFormat.OpenXml
```

That's it. No System.Drawing.Common, no ImageSharp, no COM. Cross-platform.

## Core Capabilities

### StyleBuilder.BuildFromJson — One-Click Formatting
```csharp
StyleBuilder.BuildFromJson(sp.Styles, "formats/journal-paper.json");
var sectPr = StyleBuilder.LoadPageLayout("formats/journal-paper.json").Build();
// Swap format = swap JSON. Contest → journal → thesis in 3 seconds.
```

### DocumentWriter — Chainable Content
```csharp
var w = new DocumentWriter(body, doc);
w.Title("Title").EnglishTitle("English Title")
 .Abstract("Abstract...").Keywords("K1; K2")
 .TableOfContents("Contents")
 .Heading("Introduction", 1).Body("Text[1].")
 .Footnote("Note").Endnote("Endnote")
 .Table("Caption", 1, headers, rows).TableStyle(TableStyles.LightGridAccent1)
 .VariableTable("Variables", 1, variablePlanRows)
 .Figure("Caption", 1).BarChart("Chart", cats, vals)
 .Bookmark("_ref").CrossReference("_ref", "see Table 1")
 .Hyperlink("https://example.com", "Link")
 .BibHeading().References("Author. Title[J]. Journal, Year.");
```

### DocxTemplate — Fill Templates
```csharp
DocxTemplate.Fill("template.docx", "output.docx", new { Name = "Zhang", Score = 95 });
// {{tag}} replacement + @if/@foreach blocks + table row data binding
```

### ImageEmbedder — Real Images
```csharp
ImageEmbedder.EmbedImages(body, mainPart, new[] { "fig1.png", "fig2.png" });
// First 2 side-by-side in borderless table, rest stacked vertically
// ImageHeaderReader reads PNG/JPEG/GIF/BMP/TIFF dimensions — no external deps
```

### WordPreview — Generate + Diagnose
```csharp
var r = WordPreview.Preview("paper.docx");
// Text preview + 7-step diagnostics + OpenXmlValidator OOXML validation
```

### SectionBuilder — Page Layout
```csharp
var sectPr = new SectionBuilder().A4().Margins("3cm", "2.5cm", "2.5cm", "2cm").Build();
```

### TableStyles — 90+ Built-in Designs
```csharp
w.TableStyle(TableStyles.LightGridAccent1);
// Full set: LightShading, MediumGrid, Colorful, DarkList series...
```

## Paper Analysis (16 Types)

```csharp
var types = PaperTypeClassifier.Classify(text);           // Classify research design
var struct = PaperStructureExtractor.BuildPaperStructure(text); // Extract sections
var vars = VariablePlanGenerator.GenerateVariablePlan(text);    // Generate variable table
var refs = ReferenceAnalyzer.CheckReferenceRisks(text);         // Check references
var diag = PaperDiagnostics.DiagnosePaperQuality(text, ...);    // Quality diagnosis (A-E grade)
```

Diagnosis pipeline: evidence chain (10 items) → data requirements (9 items) → gap grade (A-E) → semantic diagnosis → 3-tier quality report.

## Advanced Features

```csharp
AdvancedFeatures.InsertComment(doc, "reviewer", "Fix this", para);  // Comments
AdvancedFeatures.AppendTrackedInsertion(para, "new text");           // Track changes
body.Append(AdvancedFeatures.InsertPlainTextControl("name"));         // Content controls
AdvancedFeatures.EmbedFont(doc, "font.ttf", "FontName");             // Font embedding
AdvancedFeatures.AppendDocument(target, "source.docx");               // Document merge
AdvancedFeatures.SetDocumentProperties(doc, title: "T", author: "A"); // Properties
AdvancedFeatures.ProtectDocument(doc, "readOnly");                   // Protection
```

## Quick Start

```powershell
dotnet add package Angri450.Nong.Docx
```

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocxCore;

using var doc = WordprocessingDocument.Create("paper.docx", WordprocessingDocumentType.Document);
var main = doc.AddMainDocumentPart();
main.Document = new Document(new Body());
var body = main.Document.Body!;

// Styles from JSON
var sp = main.AddNewPart<StyleDefinitionsPart>();
sp.Styles = new Styles();
StyleBuilder.BuildFromJson(sp.Styles, "formats/journal-paper.json");

// Page layout
var sectPr = StyleBuilder.LoadPageLayout("formats/journal-paper.json").Build();

var w = new DocumentWriter(body);
w.Title("Title").Abstract("Abstract...").Keywords("A; B; C")
 .Heading("Introduction", 1).Body("Text[1].")
 .BibHeading().References("Author. Title[J]. Journal, Year.");

// Images
ImageEmbedder.EmbedImages(body, main, new[] { "fig1.png", "fig2.png" });

body.Append(sectPr);
ElementOrder.RectifyTree(body);
main.Document.Save();
```

## Format Templates

| Template | Body | Heading | Use Case |
|----------|------|---------|----------|
| life-sciences-contest | 宋体 10.5pt | 黑体 14pt | Contest (4-page limit) |
| journal-paper | 宋体 10.5pt | 黑体 18pt | Journal (GB/T 7714) |
| course-paper | 宋体 10.5pt | 黑体 14pt | Course paper |
| degree-thesis | 宋体 12pt | 黑体 16pt | Degree thesis |

Swap format = swap JSON. No code changes.

## License

Apache-2.0. Built on [Open XML SDK](https://github.com/dotnet/Open-XML-SDK).
