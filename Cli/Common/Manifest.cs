namespace Nong.Cli.Common;

/// <summary>
/// Command manifest. Describes every available command for nong commands --json output.
/// </summary>
public static class Manifest
{
    public sealed record CommandInfo(
        string Name,
        string Description,
        string Group,
        string[] Aliases
    );

    public static List<CommandInfo> All()
    {
        var list = new List<CommandInfo>();

        // word
        list.Add(new("word read", "Extract plain text from a .docx file", "word", []));
        list.Add(new("word preview", "7-step document structure diagnostic", "word", []));
        list.Add(new("word extract", "Extract embedded images", "word", []));
        list.Add(new("word dissect", "Format fingerprint to JSON", "word", []));
        list.Add(new("word rebuild", "Clean OOXML style pollution", "word", []));
        list.Add(new("word fill", "Template fill from JSON data", "word", []));
        list.Add(new("word stats", "Document statistics", "word", []));
        list.Add(new("word fonts", "List all fonts", "word", []));
        list.Add(new("word styles", "List all style definitions", "word", []));
        list.Add(new("word validate", "OOXML schema validation", "word", []));
        list.Add(new("word merge", "Merge two docx files", "word", []));

        // inspect / paper / refs / official (workflow aliases)
        list.Add(new("inspect classify", "Classify paper type (16 types)", "inspect", ["paper classify"]));
        list.Add(new("inspect structure", "Extract paper structure", "inspect", ["paper structure"]));
        list.Add(new("inspect diagnose", "Full paper quality diagnosis", "inspect", ["paper diagnose"]));
        list.Add(new("inspect refs", "Reference analysis and risk check", "inspect", ["refs check"]));
        list.Add(new("inspect varplan", "Variable operationalization plan", "inspect", ["paper varplan"]));
        list.Add(new("inspect write paper", "Generate paper from JSON spec", "inspect", ["paper write"]));
        list.Add(new("inspect write official", "Generate official document from JSON spec", "inspect", ["official write"]));
        list.Add(new("inspect write letter", "Generate letter from JSON spec", "inspect", []));
        list.Add(new("official format", "Format docx to GB/T 9704 standard", "inspect", []));

        // chart
        list.Add(new("chart bar", "Bar chart with error bars and significance", "chart", []));
        list.Add(new("chart line", "Line chart", "chart", []));
        list.Add(new("chart scatter", "Scatter plot", "chart", []));
        list.Add(new("chart pie", "Pie chart", "chart", []));
        list.Add(new("chart anova", "One-way ANOVA", "chart", ["stats anova"]));
        list.Add(new("chart duncan", "Duncan MRT post-hoc test", "chart", ["stats duncan"]));

        // diagram
        list.Add(new("diagram flowchart", "Flowchart from JSON spec", "diagram", []));
        list.Add(new("diagram network", "Network graph from JSON spec", "diagram", []));
        list.Add(new("diagram tree", "Phylogenetic tree from Newick", "diagram", []));

        // excel
        list.Add(new("excel read", "Read xlsx content", "excel", []));
        list.Add(new("excel sheets", "List worksheets", "excel", []));
        list.Add(new("excel create", "Create blank xlsx", "excel", []));

        // pptx
        list.Add(new("pptx read", "Extract slide text", "pptx", []));
        list.Add(new("pptx slides", "List slide structure", "pptx", []));

        // ocr
        list.Add(new("ocr local", "Local PaddleOCR", "ocr", []));
        list.Add(new("ocr cloud", "Cloud PaddleOCR-VL", "ocr", []));

        // icons
        list.Add(new("icons list", "List all Bioicons", "icons", []));
        list.Add(new("icons search", "Search Bioicons", "icons", []));

        // genre
        list.Add(new("genre list", "List format templates", "genre", []));
        list.Add(new("genre show", "Show template content", "genre", []));

        return list;
    }
}
