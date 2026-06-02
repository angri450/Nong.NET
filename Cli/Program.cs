using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text.Json;
using Nong.Cli.Commands;
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
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
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

        // === Real command groups ===
        root.AddCommand(WordCommands.Create(jsonOpt));

        // === Stub command groups ===
        root.AddCommand(CreateStubGroup("inspect", "Content inspection and document writing", jsonOpt,
            ("classify", "Classify paper type (16 types)"),
            ("structure", "Extract paper structure"),
            ("diagnose", "Full paper quality diagnosis"),
            ("refs", "Reference analysis and risk check"),
            ("varplan", "Variable operationalization plan"),
            ("evidence", "Evidence chain diagnosis"),
            ("data-req", "Data requirements diagnosis"),
            ("gap", "Gap grade assessment"),
            ("semantics", "Semantic diagnosis")
        ));

        root.AddCommand(CreateStubGroup("chart", "Statistical charts and analysis", jsonOpt,
            ("bar", "Bar chart with error bars and significance"),
            ("line", "Line chart"),
            ("scatter", "Scatter plot"),
            ("pie", "Pie chart"),
            ("anova", "One-way ANOVA"),
            ("duncan", "Duncan MRT post-hoc test")
        ));

        root.AddCommand(CreateStubGroup("diagram", "Scientific diagrams", jsonOpt,
            ("flowchart", "Flowchart from JSON spec"),
            ("network", "Network graph from JSON spec"),
            ("tree", "Phylogenetic tree from Newick")
        ));

        root.AddCommand(CreateStubGroup("excel", "Excel spreadsheet operations", jsonOpt,
            ("read", "Read xlsx content"),
            ("sheets", "List worksheets"),
            ("create", "Create blank xlsx")
        ));

        root.AddCommand(CreateStubGroup("pptx", "PowerPoint operations", jsonOpt,
            ("read", "Extract slide text"),
            ("slides", "List slide structure")
        ));

        root.AddCommand(CreateStubGroup("ocr", "OCR operations", jsonOpt,
            ("local", "Local PaddleOCR"),
            ("cloud", "Cloud PaddleOCR-VL")
        ));

        root.AddCommand(CreateStubGroup("icons", "Bioicons operations", jsonOpt,
            ("list", "List all Bioicons"),
            ("search", "Search Bioicons")
        ));

        root.AddCommand(CreateStubGroup("genre", "Format template library", jsonOpt,
            ("list", "List format templates"),
            ("show", "Show template content")
        ));

        // === Workflow aliases ===
        root.AddCommand(CreateStubGroup("paper", "Paper operations (alias: inspect)", jsonOpt,
            ("classify", "Classify paper type"),
            ("structure", "Extract paper structure"),
            ("diagnose", "Full paper quality diagnosis"),
            ("varplan", "Variable operationalization plan"),
            ("write", "Generate paper from JSON spec")
        ));

        root.AddCommand(CreateStubGroup("refs", "Reference operations (alias: inspect refs)", jsonOpt,
            ("check", "Reference analysis and risk check")
        ));

        root.AddCommand(CreateStubGroup("official", "Official document operations", jsonOpt,
            ("write", "Generate official document"),
            ("format", "Format docx to GB/T 9704 standard")
        ));

        root.AddCommand(CreateStubGroup("stats", "Statistical operations (alias: chart)", jsonOpt,
            ("anova", "One-way ANOVA"),
            ("duncan", "Duncan MRT")
        ));

        var builder = new CommandLineBuilder(root)
            .UseDefaults()
            .Build();
        return await builder.InvokeAsync(args);
    }

    static Command CreateStubGroup(string name, string description, Option<bool> jsonOpt,
        params (string name, string desc)[] subs)
    {
        var cmd = new Command(name, description);
        foreach (var (n, d) in subs)
        {
            var sub = new Command(n, d);
            CliHelpers.SetNotImplemented(sub, d, jsonOpt);
            cmd.AddCommand(sub);
        }
        return cmd;
    }
}
