using System.CommandLine;
using System.Text.Json;
using ChartCore;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>
/// Chart command group: stats anova + duncan (phase 3), others stubbed.
/// </summary>
public static class ChartCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("chart", "Statistical charts and analysis");

        cmd.AddCommand(CreateAnova(jsonOpt));
        cmd.AddCommand(CreateDuncan(jsonOpt));

        var stubs = new (string name, string desc)[]
        {
            ("bar", "Bar chart with error bars and significance"),
            ("line", "Line chart"),
            ("scatter", "Scatter plot"),
            ("pie", "Pie chart"),
        };
        foreach (var (n, d) in stubs)
        {
            var c = new Command(n, d);
            CliHelpers.SetNotImplemented(c, d, jsonOpt);
            cmd.AddCommand(c);
        }

        return cmd;
    }

    // ===== stats anova =====

    static Command CreateAnova(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json)");
        var cmd = new Command("anova", "One-way ANOVA") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null)
            {
                Environment.ExitCode = CliHelpers.WriteError("stats anova", err, json);
                return;
            }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var groups = DataLoader.FromJson(file);
                    return StatsEngine.OneWayAnova(groups);
                });

                if (json)
                {
                    var data = new
                    {
                        f = result.F, p = result.P,
                        ssb = result.SSB, ssw = result.SSW, sst = result.SST,
                        msb = result.MSB, msw = result.MSW,
                        dfb = result.dfB, dfw = result.dfW,
                        groups = result.GroupStats.Select(g => new
                        {
                            name = g.Key, n = g.Value.N, mean = g.Value.Mean,
                            sd = g.Value.SD, sem = g.Value.SEM,
                            min = g.Value.Min, max = g.Value.Max
                        })
                    };
                    var output = JsonOutput.Ok("stats anova",
                        $"F={result.F:F2}, P={result.P:F4}, df=({result.dfB},{result.dfW})",
                        data);
                    output.Metrics["groups"] = result.GroupStats.Count;
                    output.Metrics["totalN"] = result.N;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"=== One-Way ANOVA ===");
                    Console.WriteLine($"F = {result.F:F4}, P = {result.P:F6}");
                    Console.WriteLine($"SSB = {result.SSB:F2}, SSW = {result.SSW:F2}, SST = {result.SST:F2}");
                    Console.WriteLine($"dfB = {result.dfB}, dfW = {result.dfW}");
                    Console.WriteLine($"MSB = {result.MSB:F4}, MSW = {result.MSW:F4}");
                    Console.WriteLine();
                    Console.WriteLine($"{"Group",-16} {"N",4} {"Mean",10} {"SD",10} {"SEM",10} {"Min",8} {"Max",8}");
                    foreach (var g in result.GroupStats)
                        Console.WriteLine($"{g.Key,-16} {g.Value.N,4} {g.Value.Mean,10:F2} {g.Value.SD,10:F2} {g.Value.SEM,10:F2} {g.Value.Min,8:F2} {g.Value.Max,8:F2}");
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = CliHelpers.WriteError("stats anova",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }

            Environment.ExitCode = 0;
        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== stats duncan =====

    static Command CreateDuncan(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json)");
        var alphaOpt = new Option<double>("--alpha", () => 0.05, "Significance level");
        var cmd = new Command("duncan", "Duncan MRT post-hoc test") { fileArg, alphaOpt };

        cmd.SetHandler((string file, double alpha, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null)
            {
                Environment.ExitCode = CliHelpers.WriteError("stats duncan", err, json);
                return;
            }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var groups = DataLoader.FromJson(file);
                    var anova = StatsEngine.OneWayAnova(groups);
                    return StatsEngine.DuncanMRT(groups, anova.MSW, anova.dfW, alpha);
                });

                if (json)
                {
                    var data = new
                    {
                        alpha = result.Alpha,
                        mse = result.MSE,
                        dfError = result.dfError,
                        groups = result.Groups.Select(g => new
                        {
                            label = g.Label, mean = g.Mean, sd = g.SD,
                            significance = g.Significance
                        })
                    };
                    var output = JsonOutput.Ok("stats duncan",
                        $"Duncan MRT: {result.Groups.Count} groups, alpha={alpha}",
                        data);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"=== Duncan MRT (alpha={alpha}) ===");
                    Console.WriteLine($"MSE = {result.MSE:F4}, dfError = {result.dfError}");
                    Console.WriteLine();
                    Console.WriteLine($"{"Group",-16} {"Mean",10} {"SD",8}  Significance");
                    foreach (var g in result.Groups)
                        Console.WriteLine($"{g.Label,-16} {g.Mean,10:F2} {g.SD,8:F2}  {g.Significance}");
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = CliHelpers.WriteError("stats duncan",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }

            Environment.ExitCode = 0;
        }, fileArg, alphaOpt, jsonOpt);

        return cmd;
    }
}
