namespace Nong.Cli.Common;

/// <summary>
/// Command manifest. Describes every available command for nong commands --json output.
/// Status: implemented | stub | planned
/// </summary>
public static class Manifest
{
    public sealed record CommandInfo(
        string Name,
        string Description,
        string Group,
        string[] Aliases,
        string Status = "implemented"
    );

    public static List<CommandInfo> All()
    {
        var list = new List<CommandInfo>();

        // === word ===
        list.Add(new("word check", "Preflight .doc/.docx before conversion, slicing, or editing", "word", []));
        list.Add(new("word convert", "Convert legacy .doc to .docx as a boundary step", "word", []));
        list.Add(new("word create", "Create DOCX directly from authored NongMark", "word", []));
        list.Add(new("word read", "Extract plain text from a .docx file", "word", []));
        list.Add(new("word preview", "7-step document structure diagnostic", "word", ["word diagnose"]));
        list.Add(new("word fill", "Template fill from JSON data", "word", []));
        list.Add(new("word rebuild", "Clean OOXML style pollution", "word", ["word clean-styles"]));
        list.Add(new("word extract", "Extract embedded images", "word", []));
        list.Add(new("word dissect", "Format fingerprint or NongPandoc package slice", "word", []));
        list.Add(new("word stats", "Document statistics", "word", []));
        list.Add(new("word fonts", "List all fonts", "word", []));
        list.Add(new("word styles", "List all style definitions", "word", []));
        list.Add(new("word validate", "OOXML schema validation", "word", []));
        list.Add(new("word merge", "Merge two docx files", "word", []));
        list.Add(new("word outline", "Extract document outline", "word", []));
        list.Add(new("word images", "List, extract, analyze, and auto-crop images with content-aware border detection", "word", []));
        list.Add(new("word crop", "Auto-crop blank margins from all images using content-aware border detection", "word", []));
        list.Add(new("word fit-images", "Scale inline multi-image paragraphs so images fit side-by-side within page width", "word", ["word compact-images"]));
        list.Add(new("word compact-tables", "Compact tables: remove fixed row heights, equalize column widths, center on page", "word", ["word tables"]));
        list.Add(new("word regroup-images", "Merge orphan images across paragraphs for side-by-side layout, then scale to fit", "word", ["word pair-images"]));
        list.Add(new("word estimate", "Estimate page breaks and measure blank space on each page", "word", ["word pages"]));
        list.Add(new("word page-setup", "Set page size, orientation, margins, columns, different first page", "word", ["word layout"]));
        list.Add(new("word indent", "Set paragraph indentation: first-line, hanging, left, right, outline level", "word", []));
        list.Add(new("word paragraph-control", "Set pagination controls: keepNext, keepLines, pageBreakBefore, widowControl", "word", []));
        list.Add(new("word image-wrap", "Convert inline images to floating with configurable text wrap modes", "word", ["word wrap"]));
        list.Add(new("word cell-format", "Format table cells: borders, shading, alignment, padding", "word", []));
        list.Add(new("word run-format", "Character-level formatting: underline, strikethrough, color, highlight, spacing", "word", ["word char-format"]));
        list.Add(new("word comments", "Read document comments", "word", []));
        list.Add(new("word revisions", "List tracked changes", "word", []));
        list.Add(new("word infer-format", "Infer OpenXML format from Chinese description", "word", []));
        list.Add(new("word fix-order", "Internal OOXML/structure repair only; not a visible formatting repair", "word", []));
        list.Add(new("word academic-format", "Visible academic Word formatting repair for headings, body text, tables, fonts, and spacing", "word", []));
        list.Add(new("word format-gongwen", "Apply Chinese official-document formatting to an existing DOCX", "word", []));
        list.Add(new("word format-audit", "Read-only visible Word formatting evidence audit for headings, body text, tables, fonts, spacing, and CI gates", "word", []));
        list.Add(new("word repair-plan", "Explain which Word repair command to use and how to verify visible formatting repairs", "word", []));
        list.Add(new("word table-reflow", "Explicitly split long or wide tables into continuation tables", "word", []));
        list.Add(new("word protect", "Apply document protection", "word", []));
        list.Add(new("word embed-font", "Embed font into document", "word", []));
        list.Add(new("word add paragraph", "Append paragraph to document", "word", ["word add-paragraph"]));
        list.Add(new("word add table", "Append table to document", "word", ["word add-table"]));
        list.Add(new("word add footnote", "Append footnote to document", "word", ["word add-footnote"]));
        list.Add(new("word add endnote", "Append endnote to document", "word", ["word add-endnote"]));
        list.Add(new("word add image", "Append image to document", "word", ["word add-image"]));
        list.Add(new("word add toc", "Append table of contents to document", "word", ["word add-toc"]));
        list.Add(new("word add xref", "Append cross-reference to document", "word", ["word add-xref"]));
        list.Add(new("word add link", "Append hyperlink to document", "word", ["word add-link"]));
        list.Add(new("word add bookmark", "Append bookmark to document", "word", ["word add-bookmark"]));
        list.Add(new("word add comment", "Append comment to document", "word", ["word add-comment"]));
        list.Add(new("word add math", "Append math equation to document", "word", ["word add-math"]));
        list.Add(new("word compare", "Compare two DOCX files paragraph-by-paragraph", "word", []));

        // === inspect (implemented: diagnose, refs, write-paper, write-official) ===
        list.Add(new("inspect diagnose", "Full paper quality diagnosis", "inspect", []));
        list.Add(new("inspect refs", "Reference analysis and risk check", "inspect", ["inspect references"]));
        list.Add(new("inspect write-paper", "Generate paper from JSON spec", "inspect", []));
        list.Add(new("inspect write-official", "Generate official-document DOCX from JSON spec", "inspect", []));
        list.Add(new("inspect official-check", "Audit official-document DOCX format compliance", "inspect", []));

        // === inspect (implemented) ===
        list.Add(new("inspect classify", "Classify paper type (16 types)", "inspect", []));
        list.Add(new("inspect structure", "Extract paper structure", "inspect", []));
        list.Add(new("inspect varplan", "Variable operationalization plan", "inspect", ["inspect variables"]));
        list.Add(new("inspect evidence", "Evidence chain diagnosis", "inspect", []));
        list.Add(new("inspect data-req", "Data requirements diagnosis", "inspect", ["inspect data-requirements"]));
        list.Add(new("inspect gap", "Gap grade assessment", "inspect", []));
        list.Add(new("inspect semantics", "Semantic diagnosis", "inspect", []));

        // === chart (implemented: 11 commands) ===
        list.Add(new("chart analyze", "Full analysis: ANOVA + Duncan + stats", "chart", []));
        list.Add(new("chart bar", "Bar chart with error bars and significance", "chart", []));
        list.Add(new("chart anova", "One-way ANOVA", "chart", []));
        list.Add(new("chart duncan", "Duncan MRT post-hoc test", "chart", []));
        list.Add(new("chart line", "Line chart", "chart", []));
        list.Add(new("chart scatter", "Scatter plot", "chart", []));
        list.Add(new("chart pie", "Pie chart", "chart", []));
        list.Add(new("chart boxplot", "Box plot for treatment group distribution comparison", "chart", []));
        list.Add(new("chart histogram", "Histogram for data distribution", "chart", []));
        list.Add(new("chart heatmap", "Heatmap chart from 2D data", "chart", []));
        list.Add(new("chart radar", "Radar/spider chart for multi-index comparison", "chart", []));

        // === excel (implemented: 8 commands) ===
        list.Add(new("excel sheets", "List worksheets", "excel", []));
        list.Add(new("excel read", "Read xlsx content", "excel", []));
        list.Add(new("excel to-groups", "Convert Excel columns to grouped data", "excel", []));
        list.Add(new("excel create", "Create xlsx from JSON spec", "excel", []));
        list.Add(new("excel dissect", "Slice xlsx into a NongPandoc package", "excel", []));
        list.Add(new("excel style", "Apply cell styles from a JSON spec", "excel", []));
        list.Add(new("excel formula", "Write formulas from a JSON spec", "excel", []));
        list.Add(new("excel pivot", "Create a pivot table from a JSON spec", "excel", []));

        // === diagram (implemented: flowchart, network, tree) ===
        list.Add(new("diagram flowchart", "Flowchart from JSON spec", "diagram", []));
        list.Add(new("diagram network", "Network graph from JSON spec", "diagram", []));
        list.Add(new("diagram tree", "Phylogenetic tree from Newick", "diagram", []));

        // === genre (implemented) ===
        list.Add(new("genre list", "List format templates", "genre", []));
        list.Add(new("genre show", "Show template content", "genre", []));

        // === icons (implemented) ===
        list.Add(new("icons list", "List all Bioicons", "icons", []));
        list.Add(new("icons search", "Search Bioicons", "icons", []));

        // === skill (implemented: validate, scan, inventory, package) ===
        list.Add(new("skill validate", "Validate SKILL.md structure and references", "skill", []));
        list.Add(new("skill scan", "Security scan for skill directories", "skill", []));
        list.Add(new("skill inventory", "List skill directory contents", "skill", []));
        list.Add(new("skill package", "Validate + scan + package skill into .zip", "skill", []));

        // === pptx (implemented: read, slides, dissect) ===
        list.Add(new("pptx read", "Extract slide text", "pptx", []));
        list.Add(new("pptx slides", "List slide structure", "pptx", []));
        list.Add(new("pptx dissect", "Slice pptx into a NongPandoc package", "pptx", []));
        list.Add(new("pptx create", "Create pptx from JSON spec", "pptx", []));

        // === ocr (implemented: cloud, local, check-env, analyze-image, models, install-model, to-word) ===
        list.Add(new("ocr local", "Local PP-OCRv5 Chinese text recognition through pure .NET runtime", "ocr", []));
        list.Add(new("ocr cloud", "Cloud PaddleOCR-VL-1.6 via PADDLEOCR_ACCESS_TOKEN", "ocr", []));
        list.Add(new("ocr check-env", "Check OCR environment status", "ocr", []));
        list.Add(new("ocr analyze-image", "Analyze image structure (no OCR, no token)", "ocr", []));
        list.Add(new("ocr models", "List available OCR models", "ocr", []));
        list.Add(new("ocr install-model", "Install/check first-party PP-OCRv5 platform native runtime bundle from Huawei NuGet/cache", "ocr", []));
        list.Add(new("ocr to-word", "Convert image/PDF to Word document via cloud OCR", "ocr", []));

        // === pdf (implemented: 7 commands) ===
        list.Add(new("pdf check", "Preflight PDF and classify text/hybrid/scan route", "pdf", []));
        list.Add(new("pdf dissect", "Slice PDF into a NongPandoc package", "pdf", []));
        list.Add(new("pdf render", "Render PDF pages to PNG images through local PDFium runtime", "pdf", []));
        list.Add(new("pdf images", "Extract embedded PDF images with page/bbox provenance", "pdf", []));
        list.Add(new("pdf merge", "Merge multiple PDF files into one", "pdf", []));
        list.Add(new("pdf split", "Split PDF pages into a separate document", "pdf", []));
        list.Add(new("pdf ocr", "Add rendered image layer to scanned PDF pages", "pdf", []));
        list.Add(new("pdf compress", "Compress PDF by rebuilding content stream and removing unused objects", "pdf", []));

        // === lit (implemented: parse, validate, plan, search, export) ===
        list.Add(new("lit parse", "Parse CNKI-like literature retrieval DSL", "lit", []));
        list.Add(new("lit validate", "Validate CNKI-like literature retrieval DSL", "lit", []));
        list.Add(new("lit plan", "Plan provider rough queries for literature retrieval", "lit", []));
        list.Add(new("lit search", "Search legal metadata/OA literature providers with local strict filtering", "lit", []));
        list.Add(new("lit export", "Export normalized literature results as JSON, Markdown, or BibTeX", "lit", []));

        // === slice (implemented: inspect, blocks, block, assets) ===
        list.Add(new("slice inspect", "Inspect a NongPandoc package contract and AI read order", "slice", []));
        list.Add(new("slice blocks", "List content blocks from a NongPandoc package", "slice", []));
        list.Add(new("slice block", "Read one block with unified content, structure, format, diagnostics, and assets", "slice", []));
        list.Add(new("slice assets", "List assets from a NongPandoc package", "slice", []));

        // === progress (implemented: report) ===
        list.Add(new("progress report", "Generate HTML progress reports from log/plans, log/changelog, log/debug, and log/guidance", "progress", []));

        return list;
    }
}
