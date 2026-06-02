using System.CommandLine;
using System.Text.Json;
using DiagramCore;
using DiagramCore.Models;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>Diagram command group: flowchart, network (phase 7).</summary>
public static class DiagramCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("diagram", "Scientific diagrams");

        cmd.AddCommand(CreateFlowchart(jsonOpt));
        cmd.AddCommand(CreateNetwork(jsonOpt));

        var c = new Command("tree", "Phylogenetic tree from Newick");
        CliHelpers.SetNotImplemented(c, "Phylogenetic tree from Newick", jsonOpt);
        cmd.AddCommand(c);

        return cmd;
    }

    static Command CreateFlowchart(Option<bool> jsonOpt)
    {
        var specArg = new Argument<string>("spec", "Path to flowchart spec JSON");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var cmd = new Command("flowchart", "Flowchart from JSON spec") { specArg, outOpt };

        cmd.SetHandler((string spec, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(spec);
            if (err != null) { Environment.ExitCode = CliHelpers.WriteError("diagram flowchart", err, json); return; }

            try
            {
                var elapsed = CliHelpers.Time(() =>
                {
                    var jsonText = File.ReadAllText(spec);
                    DiagramBuilder.FromDsl(jsonText, output);
                });

                if (json)
                {
                    var oj = JsonOutput.Ok("diagram flowchart", $"Saved: {output}");
                    oj.Artifacts["png"] = Path.GetFullPath(output);
                    oj.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"OK: {Path.GetFullPath(output)}");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = CliHelpers.WriteError("diagram flowchart", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
            Environment.ExitCode = 0;
        }, specArg, outOpt, jsonOpt);

        return cmd;
    }

    static Command CreateNetwork(Option<bool> jsonOpt)
    {
        var specArg = new Argument<string>("spec", "Path to network graph spec JSON");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var cmd = new Command("network", "Network graph from JSON spec") { specArg, outOpt };

        cmd.SetHandler((string spec, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(spec);
            if (err != null) { Environment.ExitCode = CliHelpers.WriteError("diagram network", err, json); return; }

            try
            {
                var elapsed = CliHelpers.Time(() =>
                {
                    var graph = JsonSerializer.Deserialize<Graph>(File.ReadAllText(spec),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    DiagramBuilder.NetworkGraph(graph!, output);
                });

                if (json)
                {
                    var oj = JsonOutput.Ok("diagram network", $"Saved: {output}");
                    oj.Artifacts["png"] = Path.GetFullPath(output);
                    oj.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"OK: {Path.GetFullPath(output)}");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = CliHelpers.WriteError("diagram network", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
            Environment.ExitCode = 0;
        }, specArg, outOpt, jsonOpt);

        return cmd;
    }
}
