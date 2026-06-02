using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text.Json;
using Nong.Cli.Common;

namespace Nong.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("nong — Nong.NET CLI toolkit for document generation and inspection.");

        // === Global options ===
        var jsonOpt = new Option<bool>("--json", () => false, "Output structured JSON");
        var verboseOpt = new Option<bool>("--verbose", () => false, "Verbose output");
        root.AddGlobalOption(jsonOpt);
        root.AddGlobalOption(verboseOpt);

        // === nong --version ===
        root.SetHandler(() =>
        {
            Console.WriteLine("nong v3.1.0");
        });

        // === nong commands --json ===
        var commandsCmd = new Command("commands", "List all available commands");
        commandsCmd.SetHandler((bool json) =>
        {
            var manifest = Manifest.All();
            if (json)
            {
                var output = JsonOutput.Ok("commands", $"{manifest.Count} commands available", manifest);
                Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                foreach (var c in manifest)
                {
                    var aliasStr = c.Aliases.Length > 0 ? $" (alias: {string.Join(", ", c.Aliases)})" : "";
                    Console.WriteLine($"{c.Name,-35} {c.Description}{aliasStr}");
                }
            }
        }, jsonOpt);
        root.AddCommand(commandsCmd);

        // === Register all subcommand groups ===
        RegisterWord(root, jsonOpt);
        RegisterInspect(root, jsonOpt);
        RegisterChart(root, jsonOpt);
        RegisterDiagram(root, jsonOpt);
        RegisterExcel(root, jsonOpt);
        RegisterPptx(root, jsonOpt);
        RegisterOcr(root, jsonOpt);
        RegisterIcons(root, jsonOpt);
        RegisterGenre(root, jsonOpt);

        // === Workflow aliases (top-level shortcuts) ===
        RegisterPaperAlias(root, jsonOpt);
        RegisterRefsAlias(root, jsonOpt);
        RegisterOfficialAlias(root, jsonOpt);
        RegisterStatsAlias(root, jsonOpt);

        var builder = new CommandLineBuilder(root)
            .UseDefaults()
            .Build();
        return await builder.InvokeAsync(args);
    }

    // ===== Word =====

    static void RegisterWord(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("word", "Word document operations");
        AddEmptySub(cmd, "read", "Extract plain text from a .docx file", jsonOpt);
        AddEmptySub(cmd, "preview", "7-step document structure diagnostic", jsonOpt);
        AddEmptySub(cmd, "extract", "Extract embedded images", jsonOpt);
        AddEmptySub(cmd, "dissect", "Format fingerprint to JSON", jsonOpt);
        AddEmptySub(cmd, "rebuild", "Clean OOXML style pollution", jsonOpt);
        AddEmptySub(cmd, "fill", "Template fill from JSON data", jsonOpt);
        AddEmptySub(cmd, "stats", "Document statistics", jsonOpt);
        AddEmptySub(cmd, "fonts", "List all fonts", jsonOpt);
        AddEmptySub(cmd, "styles", "List all style definitions", jsonOpt);
        AddEmptySub(cmd, "validate", "OOXML schema validation", jsonOpt);
        AddEmptySub(cmd, "merge", "Merge two docx files", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== Inspect =====

    static void RegisterInspect(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("inspect", "Content inspection and document writing");
        AddEmptySub(cmd, "classify", "Classify paper type (16 types)", jsonOpt);
        AddEmptySub(cmd, "structure", "Extract paper structure", jsonOpt);
        AddEmptySub(cmd, "diagnose", "Full paper quality diagnosis", jsonOpt);
        AddEmptySub(cmd, "refs", "Reference analysis and risk check", jsonOpt);
        AddEmptySub(cmd, "varplan", "Variable operationalization plan", jsonOpt);
        AddEmptySub(cmd, "evidence", "Evidence chain diagnosis", jsonOpt);
        AddEmptySub(cmd, "data-req", "Data requirements diagnosis", jsonOpt);
        AddEmptySub(cmd, "gap", "Gap grade assessment", jsonOpt);
        AddEmptySub(cmd, "semantics", "Semantic diagnosis", jsonOpt);
        root.AddCommand(cmd);

        // inspect write subcommands
        var writeCmd = new Command("write", "Generate documents from JSON spec");
        AddEmptySub(writeCmd, "paper", "Generate paper docx from JSON spec", jsonOpt);
        AddEmptySub(writeCmd, "official", "Generate official document docx from JSON spec", jsonOpt);
        AddEmptySub(writeCmd, "letter", "Generate letter docx from JSON spec", jsonOpt);
        cmd.AddCommand(writeCmd);

        // inspect refs subcommands
        var refsCmd = cmd.Subcommands.First(c => c.Name == "refs");
        AddEmptySub(refsCmd, "resolve", "Resolve [@key] citations to [N]", jsonOpt);
        AddEmptySub(refsCmd, "generate", "Generate formatted reference list", jsonOpt);
    }

    // ===== Chart =====

    static void RegisterChart(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("chart", "Statistical charts and analysis");
        AddEmptySub(cmd, "bar", "Bar chart with error bars and significance", jsonOpt);
        AddEmptySub(cmd, "line", "Line chart", jsonOpt);
        AddEmptySub(cmd, "scatter", "Scatter plot", jsonOpt);
        AddEmptySub(cmd, "pie", "Pie chart", jsonOpt);
        AddEmptySub(cmd, "anova", "One-way ANOVA", jsonOpt);
        AddEmptySub(cmd, "duncan", "Duncan MRT post-hoc test", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== Diagram =====

    static void RegisterDiagram(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("diagram", "Scientific diagrams");
        AddEmptySub(cmd, "flowchart", "Flowchart from JSON spec", jsonOpt);
        AddEmptySub(cmd, "network", "Network graph from JSON spec", jsonOpt);
        AddEmptySub(cmd, "tree", "Phylogenetic tree from Newick", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== Excel =====

    static void RegisterExcel(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("excel", "Excel spreadsheet operations");
        AddEmptySub(cmd, "read", "Read xlsx content", jsonOpt);
        AddEmptySub(cmd, "sheets", "List worksheets", jsonOpt);
        AddEmptySub(cmd, "create", "Create blank xlsx", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== Pptx =====

    static void RegisterPptx(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("pptx", "PowerPoint operations");
        AddEmptySub(cmd, "read", "Extract slide text", jsonOpt);
        AddEmptySub(cmd, "slides", "List slide structure", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== OCR =====

    static void RegisterOcr(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("ocr", "OCR operations");
        AddEmptySub(cmd, "local", "Local PaddleOCR", jsonOpt);
        AddEmptySub(cmd, "cloud", "Cloud PaddleOCR-VL", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== Icons =====

    static void RegisterIcons(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("icons", "Bioicons operations");
        AddEmptySub(cmd, "list", "List all Bioicons", jsonOpt);
        AddEmptySub(cmd, "search", "Search Bioicons", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== Genre =====

    static void RegisterGenre(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("genre", "Format template library");
        AddEmptySub(cmd, "list", "List format templates", jsonOpt);
        AddEmptySub(cmd, "show", "Show template content", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== Workflow Aliases =====

    static void RegisterPaperAlias(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("paper", "Paper operations (alias: inspect)");
        AddEmptySub(cmd, "classify", "Classify paper type", jsonOpt);
        AddEmptySub(cmd, "structure", "Extract paper structure", jsonOpt);
        AddEmptySub(cmd, "diagnose", "Full paper quality diagnosis", jsonOpt);
        AddEmptySub(cmd, "varplan", "Variable operationalization plan", jsonOpt);
        AddEmptySub(cmd, "write", "Generate paper from JSON spec", jsonOpt);
        root.AddCommand(cmd);
    }

    static void RegisterRefsAlias(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("refs", "Reference operations (alias: inspect refs)");
        AddEmptySub(cmd, "check", "Reference analysis and risk check", jsonOpt);
        root.AddCommand(cmd);
    }

    static void RegisterOfficialAlias(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("official", "Official document operations (alias: inspect)");
        AddEmptySub(cmd, "write", "Generate official document", jsonOpt);
        AddEmptySub(cmd, "format", "Format docx to GB/T 9704 standard", jsonOpt);
        root.AddCommand(cmd);
    }

    static void RegisterStatsAlias(RootCommand root, Option<bool> jsonOpt)
    {
        var cmd = new Command("stats", "Statistical operations (alias: chart)");
        AddEmptySub(cmd, "anova", "One-way ANOVA", jsonOpt);
        AddEmptySub(cmd, "duncan", "Duncan MRT", jsonOpt);
        root.AddCommand(cmd);
    }

    // ===== Helper =====

    static void AddEmptySub(Command parent, string name, string description, Option<bool> jsonOpt)
    {
        var cmd = new Command(name, description);
        cmd.SetHandler((bool json) =>
        {
            if (json)
            {
                var output = JsonOutput.Ok(
                    $"{GetFullName(cmd)}",
                    $"(not yet implemented) {description}");
                Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"[nong {GetFullName(cmd)}] Not yet implemented. {description}");
            }
        }, jsonOpt);
        parent.AddCommand(cmd);
    }

    static string GetFullName(Command cmd)
    {
        var parts = new List<string>();
        var current = cmd;
        while (current != null && current is not RootCommand)
        {
            parts.Insert(0, current.Name);
            current = current.Parents.OfType<Command>().FirstOrDefault();
        }
        return string.Join(" ", parts);
    }
}
