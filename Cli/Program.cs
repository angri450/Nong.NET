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
        root.AddCommand(InspectCommands.Create(jsonOpt));
        root.AddCommand(ChartCommands.Create(jsonOpt));
        root.AddCommand(ExcelCommands.Create(jsonOpt));
        root.AddCommand(DiagramCommands.Create(jsonOpt));
        root.AddCommand(PptxCommands.Create(jsonOpt));
        root.AddCommand(GenreCommands.Create(jsonOpt));
        root.AddCommand(IconsCommands.Create(jsonOpt));

        // === Stub command groups ===
        root.AddCommand(CreateStubGroup("ocr", "OCR operations", jsonOpt,
            ("local", "Local PaddleOCR"),
            ("cloud", "Cloud PaddleOCR-VL")
        ));

        // === Workflow aliases ===
        root.AddCommand(CreateStubGroup("paper", "Paper operations (use: nong inspect <cmd>)", jsonOpt,
            ("classify", "Classify paper type"),
            ("structure", "Extract paper structure"),
            ("diagnose", "Full paper quality diagnosis"),
            ("varplan", "Variable operationalization plan"),
            ("write", "Generate paper from JSON spec")
        ));

        root.AddCommand(CreateStubGroup("refs", "Reference operations (use: nong inspect refs)", jsonOpt,
            ("check", "Reference analysis and risk check")
        ));

        root.AddCommand(CreateStubGroup("official", "Official document operations", jsonOpt,
            ("write", "Generate official document"),
            ("format", "Format docx to GB/T 9704 standard")
        ));

        root.AddCommand(CreateStubGroup("stats", "Statistical operations (use: nong chart <cmd>)", jsonOpt,
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
