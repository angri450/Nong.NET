using System.CommandLine;
using System.Text.Json;
using DocxCore;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>
/// Word command group: read and preview (phase 2), others stubbed.
/// </summary>
public static class WordCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("word", "Word document operations");

        // === Real commands (phase 2) ===
        cmd.AddCommand(CreateRead(jsonOpt));
        cmd.AddCommand(CreatePreview(jsonOpt));

        // === Stub commands ===
        var stubs = new (string name, string desc)[]
        {
            ("extract", "Extract embedded images"),
            ("dissect", "Format fingerprint to JSON"),
            ("rebuild", "Clean OOXML style pollution"),
            ("fill", "Template fill from JSON data"),
            ("stats", "Document statistics"),
            ("fonts", "List all fonts"),
            ("styles", "List all style definitions"),
            ("validate", "OOXML schema validation"),
            ("merge", "Merge two docx files"),
        };
        foreach (var (name, desc) in stubs)
        {
            var c = new Command(name, desc);
            CliHelpers.SetNotImplemented(c, desc, jsonOpt);
            cmd.AddCommand(c);
        }

        return cmd;
    }

    // ===== word read =====

    static Command CreateRead(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("read", "Extract plain text from a .docx file") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null)
            {
                Environment.ExitCode = CliHelpers.WriteError("word read", err, json);
                return;
            }

            var (result, elapsed) = CliHelpers.Time(() => WordTextReader.Read(file));

            if (json)
            {
                var data = new
                {
                    text = result.Text,
                    paragraphs = result.Paragraphs,
                    tables = result.Tables,
                    footnotes = result.Footnotes,
                    endnotes = result.Endnotes
                };
                var metrics = new Dictionary<string, object>
                {
                    ["characters"] = result.Text.Length,
                    ["paragraphs"] = result.Paragraphs.Count,
                    ["tables"] = result.Tables.Count,
                    ["footnotes"] = result.Footnotes.Count,
                    ["endnotes"] = result.Endnotes.Count
                };
                var output = JsonOutput.Ok("word read", $"Extracted {result.Paragraphs.Count} paragraphs, {result.Tables.Count} tables", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.Write(result.Text);
            }

            Environment.ExitCode = 0;
        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== word preview =====

    static Command CreatePreview(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("preview", "7-step document structure diagnostic") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null)
            {
                Environment.ExitCode = CliHelpers.WriteError("word preview", err, json);
                return;
            }

            var (pr, elapsed) = CliHelpers.Time(() => WordPreview.Preview(file));

            if (json)
            {
                var data = new
                {
                    text = pr.Text,
                    warnings = pr.Warnings,
                    errors = pr.Errors,
                    info = pr.Info,
                    statistics = new
                    {
                        paragraphs = pr.Statistics.Paragraphs,
                        tables = pr.Statistics.Tables,
                        images = pr.Statistics.Images,
                        ooxmlErrors = pr.Statistics.OoxmlErrors,
                        ooxmlWarnings = pr.Statistics.OoxmlWarnings
                    }
                };
                var metrics = new Dictionary<string, object>
                {
                    ["paragraphs"] = pr.Statistics.Paragraphs,
                    ["tables"] = pr.Statistics.Tables,
                    ["images"] = pr.Statistics.Images,
                    ["ooxml_errors"] = pr.Statistics.OoxmlErrors,
                    ["ooxml_warnings"] = pr.Statistics.OoxmlWarnings
                };
                var output = JsonOutput.Ok("word preview", $"Diagnosed: {pr.Warnings.Count} warnings, {pr.Errors.Count} errors", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine(pr.Text);
                if (pr.Errors.Count > 0)
                {
                    Console.Error.WriteLine($"=== Errors ({pr.Errors.Count}) ===");
                    foreach (var e in pr.Errors) Console.Error.WriteLine($"  [ERR] {e}");
                }
                if (pr.Warnings.Count > 0)
                {
                    Console.Error.WriteLine($"=== Warnings ({pr.Warnings.Count}) ===");
                    foreach (var w in pr.Warnings) Console.Error.WriteLine($"  [WARN] {w}");
                }
                if (pr.Info.Count > 0)
                {
                    Console.Error.WriteLine($"=== Info ({pr.Info.Count}) ===");
                    foreach (var i in pr.Info) Console.Error.WriteLine($"  [INFO] {i}");
                }
                Console.Error.WriteLine($"Stats: {pr.Statistics.Paragraphs}p {pr.Statistics.Tables}t {pr.Statistics.Images}i | OOXML errors={pr.Statistics.OoxmlErrors} warnings={pr.Statistics.OoxmlWarnings}");
            }

            Environment.ExitCode = 0;
        }, fileArg, jsonOpt);

        return cmd;
    }
}
