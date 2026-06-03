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
        var allOpt = new Option<bool>("--all", () => false, "Include stub commands");
        var commandsCmd = new Command("commands", "List available commands") { allOpt };
        commandsCmd.SetHandler((bool json, bool all) =>
        {
            var manifest = Manifest.All();
            var filtered = all ? manifest : manifest.Where(c => c.Status == "implemented").ToList();
            if (json)
            {
                var output = JsonOutput.Ok("commands", $"{filtered.Count} commands available", filtered);
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                foreach (var c in filtered)
                {
                    var aliasStr = c.Aliases.Length > 0 ? $" (alias: {string.Join(", ", c.Aliases)})" : "";
                    var stubTag = c.Status == "stub" ? " [stub]" : "";
                    Console.WriteLine($"{c.Name,-35} {c.Description}{aliasStr}{stubTag}");
                }
            }
        }, jsonOpt, allOpt);
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
        root.AddCommand(SkillCommands.Create(jsonOpt));

        // === Stub command groups ===
        root.AddCommand(CreateStubGroup("ocr", "OCR operations", jsonOpt,
            ("local", "Local PaddleOCR"),
            ("cloud", "Cloud PaddleOCR-VL")
        ));

        var builder = new CommandLineBuilder(root)
            .UseDefaults()
            .Build();
        var code = await builder.InvokeAsync(args);
        return Environment.ExitCode != 0 ? Environment.ExitCode : code;
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
