namespace Nong.Cli.Common;

/// <summary>
/// Command manifest. Describes every available command for nong commands --json output.
/// Status: implemented (20) | stub | planned
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

        // === word (implemented: read, preview, fill, rebuild) ===
        list.Add(new("word read", "Extract plain text from a .docx file", "word", []));
        list.Add(new("word preview", "7-step document structure diagnostic", "word", []));
        list.Add(new("word fill", "Template fill from JSON data", "word", []));
        list.Add(new("word rebuild", "Clean OOXML style pollution", "word", []));

        // === word (stub) ===
        list.Add(new("word extract", "Extract embedded images", "word", [], "stub"));
        list.Add(new("word dissect", "Format fingerprint to JSON", "word", [], "stub"));
        list.Add(new("word stats", "Document statistics", "word", [], "stub"));
        list.Add(new("word fonts", "List all fonts", "word", [], "stub"));
        list.Add(new("word styles", "List all style definitions", "word", [], "stub"));
        list.Add(new("word validate", "OOXML schema validation", "word", [], "stub"));
        list.Add(new("word merge", "Merge two docx files", "word", [], "stub"));

        // === inspect (implemented: diagnose, refs, write-paper) ===
        list.Add(new("inspect diagnose", "Full paper quality diagnosis", "inspect", []));
        list.Add(new("inspect refs", "Reference analysis and risk check", "inspect", []));
        list.Add(new("inspect write-paper", "Generate paper from JSON spec", "inspect", []));

        // === inspect (stub) ===
        list.Add(new("inspect classify", "Classify paper type (16 types)", "inspect", [], "stub"));
        list.Add(new("inspect structure", "Extract paper structure", "inspect", [], "stub"));
        list.Add(new("inspect varplan", "Variable operationalization plan", "inspect", [], "stub"));
        list.Add(new("inspect evidence", "Evidence chain diagnosis", "inspect", [], "stub"));
        list.Add(new("inspect data-req", "Data requirements diagnosis", "inspect", [], "stub"));
        list.Add(new("inspect gap", "Gap grade assessment", "inspect", [], "stub"));
        list.Add(new("inspect semantics", "Semantic diagnosis", "inspect", [], "stub"));

        // === chart (implemented: analyze, bar, anova, duncan) ===
        list.Add(new("chart analyze", "Full analysis: ANOVA + Duncan + stats", "chart", []));
        list.Add(new("chart bar", "Bar chart with error bars and significance", "chart", []));
        list.Add(new("chart anova", "One-way ANOVA", "chart", []));
        list.Add(new("chart duncan", "Duncan MRT post-hoc test", "chart", []));

        // === chart (stub) ===
        list.Add(new("chart line", "Line chart", "chart", [], "stub"));
        list.Add(new("chart scatter", "Scatter plot", "chart", [], "stub"));
        list.Add(new("chart pie", "Pie chart", "chart", [], "stub"));

        // === excel (implemented: sheets, read, to-groups) ===
        list.Add(new("excel sheets", "List worksheets", "excel", []));
        list.Add(new("excel read", "Read xlsx content", "excel", []));
        list.Add(new("excel to-groups", "Convert Excel columns to grouped data", "excel", []));

        // === excel (stub) ===
        list.Add(new("excel create", "Create blank xlsx", "excel", [], "stub"));

        // === diagram (implemented: flowchart, network) ===
        list.Add(new("diagram flowchart", "Flowchart from JSON spec", "diagram", []));
        list.Add(new("diagram network", "Network graph from JSON spec", "diagram", []));

        // === diagram (stub) ===
        list.Add(new("diagram tree", "Phylogenetic tree from Newick", "diagram", [], "stub"));

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

        // === stub groups ===
        list.Add(new("pptx read", "Extract slide text", "pptx", [], "stub"));
        list.Add(new("pptx slides", "List slide structure", "pptx", [], "stub"));
        list.Add(new("ocr local", "Local PaddleOCR", "ocr", [], "stub"));
        list.Add(new("ocr cloud", "Cloud PaddleOCR-VL", "ocr", [], "stub"));

        return list;
    }
}
