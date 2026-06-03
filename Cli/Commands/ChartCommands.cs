using System.Globalization;
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

        cmd.AddCommand(CreateAnalyze(jsonOpt));
        cmd.AddCommand(CreateAnova(jsonOpt));
        cmd.AddCommand(CreateDuncan(jsonOpt));
        cmd.AddCommand(CreateBarChart(jsonOpt));

        var stubs = new (string name, string desc)[]
        {
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
                CliHelpers.WriteError("stats anova", err, json);
                return;
            }

            try
            {
                var groups = DataLoader.FromJson(file);
                var verr = StatsValidation.Validate(groups, "chart");
                if (verr != null) { CliHelpers.WriteError("chart", verr, json); return; }

                var (result, elapsed) = CliHelpers.Time(() =>
                {
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
                CliHelpers.WriteError("stats anova",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }


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
                CliHelpers.WriteError("stats duncan", err, json);
                return;
            }

            try
            {
                var groups = DataLoader.FromJson(file);
                var verr2 = StatsValidation.Validate(groups, "chart");
                if (verr2 != null) { CliHelpers.WriteError("chart", verr2, json); return; }

                var (result, elapsed) = CliHelpers.Time(() =>
                {
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
                CliHelpers.WriteError("stats duncan",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
                return;
            }


        }, fileArg, alphaOpt, jsonOpt);

        return cmd;
    }

    // ===== chart analyze (P0: one-shot ANOVA + Duncan + stats) =====

    static Command CreateAnalyze(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json)");
        var alphaOpt = new Option<double>("--alpha", () => 0.05, "Significance level");
        var cmd = new Command("analyze", "Full statistical analysis (ANOVA + Duncan + descriptive stats)") { fileArg, alphaOpt };

        cmd.SetHandler((string file, double alpha, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart analyze", err, json); return; }

            try
            {
                var groups = DataLoader.FromJson(file);
                var verr3 = StatsValidation.Validate(groups, "chart");
                if (verr3 != null) { CliHelpers.WriteError("chart", verr3, json); return; }

                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    return StatsEngine.FullAnalysis(groups, alpha);
                });

                if (json)
                {
                    var data = new
                    {
                        anova = new
                        {
                            f = result.Anova.F, p = result.Anova.P,
                            ssb = result.Anova.SSB, ssw = result.Anova.SSW,
                            msb = result.Anova.MSB, msw = result.Anova.MSW,
                            dfb = result.Anova.dfB, dfw = result.Anova.dfW
                        },
                        duncan = new
                        {
                            alpha = result.Duncan.Alpha,
                            mse = result.Duncan.MSE,
                            dfError = result.Duncan.dfError,
                            groups = result.Duncan.Groups.Select(g => new
                            {
                                label = g.Label, mean = g.Mean, sd = g.SD,
                                significance = g.Significance
                            })
                        },
                        groups = result.Anova.GroupStats.Select(g => new
                        {
                            name = g.Key, n = g.Value.N, mean = g.Value.Mean,
                            sd = g.Value.SD, sem = g.Value.SEM, min = g.Value.Min, max = g.Value.Max
                        })
                    };

                    var sigGroups = string.Join(", ", result.Duncan.Groups.Select(g => $"{g.Label}{g.Significance}"));
                    var output = JsonOutput.Ok("chart analyze",
                        $"F={result.Anova.F:F2}, P={result.Anova.P:F4}, Duncan groups: {sigGroups}", data);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    result.Print();
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart analyze",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }


        }, fileArg, alphaOpt, jsonOpt);

        return cmd;
    }

    // ===== chart bar (P0: bar chart with error bars and significance) =====

    static Command CreateBarChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to data file (.json)");
        var outOpt = new Option<string>("-o", "Output PNG path") { IsRequired = true };
        var titleOpt = new Option<string>("--title", () => "", "Chart title");
        var ylabelOpt = new Option<string>("--ylabel", () => "", "Y-axis label");
        var errorOpt = new Option<string>("--error", () => "sem", "Error bar type: sem or none");
        var noSigOpt = new Option<bool>("--no-significance", () => false, "Disable Duncan significance letters");
        var cmd = new Command("bar", "Bar chart with error bars and significance letters") { fileArg, outOpt, titleOpt, ylabelOpt, errorOpt, noSigOpt };

        cmd.SetHandler((string file, string output, string title, string ylabel, string error, bool noSig, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("chart bar", err, json); return; }

            try
            {
                var showError = error != "none";

                var groups = DataLoader.FromJson(file);
                var verr4 = StatsValidation.Validate(groups, "chart");
                if (verr4 != null) { CliHelpers.WriteError("chart", verr4, json); return; }

                var (result, elapsed) = CliHelpers.Time(() =>
                {

                    // Run Duncan for significance letters if enabled
                    Dictionary<string, string>? sigLabels = null;
                    if (!noSig)
                    {
                        var anova = StatsEngine.OneWayAnova(groups);
                        if (anova.P < 0.05)
                        {
                            var duncan = StatsEngine.DuncanMRT(groups, anova.MSW, anova.dfW, 0.05);
                            sigLabels = duncan.Groups.ToDictionary(g => g.Label, g => g.Significance);
                        }
                    }

                    if (sigLabels != null)
                        ChartBuilder.BarChartWithSignificance(groups, sigLabels, title, ylabel, output, colors: null, width: 800, height: 600);
                    else
                        ChartBuilder.BarChart(groups, title, ylabel, output, colors: null, width: 800, height: 600, showErrorBar: showError, showGrid: true);

                    return (groups.Count, sigLabels);
                });

                if (json)
                {
                    var aerr = CliHelpers.CheckArtifact(output, "PNG");
                    if (aerr != null) { CliHelpers.WriteError("chart bar", aerr, json); return; }

                    var outputJson = JsonOutput.Ok("chart bar",
                        $"Bar chart saved: {output}",
                        new { groups = result.Count, hasSignificance = result.sigLabels != null });
                    outputJson.Artifacts["png"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Bar chart saved: {Path.GetFullPath(output)}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("chart bar",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }


        }, fileArg, outOpt, titleOpt, ylabelOpt, errorOpt, noSigOpt, jsonOpt);

        return cmd;
    }
}
