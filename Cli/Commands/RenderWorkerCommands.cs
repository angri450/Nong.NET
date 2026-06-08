using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using ChartCore;
using DiagramCore;
using DiagramCore.Models;
using Nong.Cli.Common;
using ScottPlot;

namespace Nong.Cli.Commands;

public static class RenderWorkerCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("__render-worker", "Internal native render worker")
        {
            IsHidden = true
        };

        var kindArg = new Argument<string>("kind");
        var actionArg = new Argument<string>("action");
        var fileOpt = new Option<string>("--file", () => "", "Input file");
        var outputOpt = new Option<string>("--output", () => "", "Output file");
        var titleOpt = new Option<string>("--title", () => "", "Title");
        var ylabelOpt = new Option<string>("--ylabel", () => "", "Y-axis label");
        var errorOpt = new Option<string>("--error", () => "sem", "Error bar type");
        var noSignificanceOpt = new Option<bool>("--no-significance", () => false, "Disable significance labels");

        cmd.AddArgument(kindArg);
        cmd.AddArgument(actionArg);
        cmd.AddOption(fileOpt);
        cmd.AddOption(outputOpt);
        cmd.AddOption(titleOpt);
        cmd.AddOption(ylabelOpt);
        cmd.AddOption(errorOpt);
        cmd.AddOption(noSignificanceOpt);

        cmd.SetHandler((InvocationContext context) =>
        {
            var kind = context.ParseResult.GetValueForArgument(kindArg);
            var action = context.ParseResult.GetValueForArgument(actionArg);
            var file = context.ParseResult.GetValueForOption(fileOpt) ?? "";
            var output = context.ParseResult.GetValueForOption(outputOpt) ?? "";
            var title = context.ParseResult.GetValueForOption(titleOpt) ?? "";
            var ylabel = context.ParseResult.GetValueForOption(ylabelOpt) ?? "";
            var error = context.ParseResult.GetValueForOption(errorOpt) ?? "sem";
            var noSignificance = context.ParseResult.GetValueForOption(noSignificanceOpt);
            var json = context.ParseResult.GetValueForOption(jsonOpt);

            switch ((kind, action))
            {
                case ("chart", "bar"):
                    RenderChartBar(file, output, title, ylabel, error, noSignificance, json);
                    break;
                case ("chart", "line"):
                    RenderChartLine(file, output, json);
                    break;
                case ("chart", "scatter"):
                    RenderChartScatter(file, output, json);
                    break;
                case ("chart", "pie"):
                    RenderChartPie(file, output, json);
                    break;
                case ("diagram", "flowchart"):
                    RenderDiagramFlowchart(file, output, json);
                    break;
                case ("diagram", "network"):
                    RenderDiagramNetwork(file, output, json);
                    break;
                case ("diagram", "tree"):
                    RenderDiagramTree(file, output, json);
                    break;
                default:
                    CliHelpers.WriteError("__render-worker",
                        ErrorCodes.ValidationFailed with { Message = $"Unknown render worker action: {kind} {action}" },
                        json);
                    break;
            }
        });

        return cmd;
    }

    static bool ValidateWorkerInput(string command, string file, string output, bool json)
    {
        var err = CliHelpers.ValidateTextFile(file);
        if (err != null)
        {
            CliHelpers.WriteError(command, err, json);
            return false;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.MissingArgument with { Message = "Output path is required." },
                json);
            return false;
        }

        CliHelpers.EnsureParentDir(output);
        return true;
    }

    static void RenderChartBar(string file, string output, string title, string ylabel, string error, bool noSignificance, bool json)
    {
        const string command = "chart bar";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var groups = DataLoader.FromJson(file);
            var validation = StatsValidation.Validate(groups, command);
            if (validation != null)
            {
                CliHelpers.WriteError(command, validation, json);
                return;
            }

            var showError = error != "none";
            var (result, elapsed) = CliHelpers.Time(() =>
            {
                Dictionary<string, string>? sigLabels = null;
                if (!noSignificance)
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

                return sigLabels;
            });

            WritePngSuccess(command, $"Bar chart saved: {Path.GetFullPath(output)}",
                new { groups = groups.Count, hasSignificance = result != null }, output, elapsed, json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void RenderChartLine(string file, string output, bool json)
    {
        const string command = "chart line";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var spec = JsonSerializer.Deserialize<LineSpec>(File.ReadAllText(file), CliHelpers.JsonOpts);
            var error = ValidateLineSpec(spec);
            if (error != null)
            {
                CliHelpers.WriteError(command, error, json);
                return;
            }

            var seriesCount = spec!.Series.Count;
            var totalPoints = spec.Series.Sum(s => s.X!.Length);
            var elapsed = CliHelpers.Time(() =>
            {
                var plt = new Plot();
                plt.Font.Set(ChartBuilder.GetCjkFontFamily());
                plt.Title(spec.Title ?? "");
                plt.XLabel(spec.XLabel ?? "");
                plt.YLabel(spec.YLabel ?? "");

                var colors = BarChartConfig.DefaultColors;
                for (var i = 0; i < spec.Series.Count; i++)
                {
                    var s = spec.Series[i];
                    var scatter = plt.Add.Scatter(s.X!, s.Y!, colors[i % colors.Length]);
                    scatter.LegendText = s.Name;
                    scatter.MarkerSize = 6;
                    scatter.LineWidth = 2;
                }

                if (spec.Series.Count > 1)
                    plt.ShowLegend();
                plt.SavePng(output, 800, 600);
            });

            WritePngSuccess(command, $"Line chart saved: {Path.GetFullPath(output)}",
                new { series = seriesCount, points = totalPoints }, output, elapsed, json);
        }
        catch (JsonException ex)
        {
            CliHelpers.WriteError(command,
                ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {ex.Message}" },
                json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void RenderChartScatter(string file, string output, bool json)
    {
        const string command = "chart scatter";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var spec = JsonSerializer.Deserialize<ScatterSpec>(File.ReadAllText(file), CliHelpers.JsonOpts);
            var error = ValidateScatterSpec(spec);
            if (error != null)
            {
                CliHelpers.WriteError(command, error, json);
                return;
            }

            var groups = new Dictionary<string, (List<double> Xs, List<double> Ys)>();
            foreach (var point in spec!.Points)
            {
                var key = string.IsNullOrWhiteSpace(point.Group) ? "default" : point.Group!;
                if (!groups.ContainsKey(key))
                    groups[key] = (new List<double>(), new List<double>());
                groups[key].Xs.Add(point.X);
                groups[key].Ys.Add(point.Y);
            }

            var pointCount = spec.Points.Count;
            var hasTrendline = spec.Trendline && spec.Points.Count >= 2;
            var elapsed = CliHelpers.Time(() =>
            {
                var plt = new Plot();
                plt.Font.Set(ChartBuilder.GetCjkFontFamily());
                plt.Title(spec.Title ?? "");
                plt.XLabel(spec.XLabel ?? "");
                plt.YLabel(spec.YLabel ?? "");

                var colors = BarChartConfig.DefaultColors;
                var groupIndex = 0;
                var showLegend = groups.Count > 1 || (groups.Count == 1 && !groups.ContainsKey("default"));

                foreach (var group in groups)
                {
                    var scatter = plt.Add.ScatterPoints(group.Value.Xs.ToArray(), group.Value.Ys.ToArray(), colors[groupIndex % colors.Length]);
                    scatter.MarkerSize = 8;
                    if (showLegend)
                        scatter.LegendText = group.Key;
                    groupIndex++;
                }

                if (hasTrendline)
                    AddTrendline(plt, spec.Points);

                if (showLegend || hasTrendline)
                    plt.ShowLegend();
                plt.SavePng(output, 800, 600);
            });

            WritePngSuccess(command, $"Scatter plot saved: {Path.GetFullPath(output)}",
                new { points = pointCount, groups = groups.Count, trendline = spec.Trendline }, output, elapsed, json);
        }
        catch (JsonException ex)
        {
            CliHelpers.WriteError(command,
                ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {ex.Message}" },
                json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void RenderChartPie(string file, string output, bool json)
    {
        const string command = "chart pie";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var spec = JsonSerializer.Deserialize<PieSpec>(File.ReadAllText(file), CliHelpers.JsonOpts);
            var error = ValidatePieSpec(spec);
            if (error != null)
            {
                CliHelpers.WriteError(command, error, json);
                return;
            }

            var slices = spec!.Values.ToDictionary(v => v.Label!, v => v.Value);
            var total = spec.Values.Sum(v => v.Value);
            var elapsed = CliHelpers.Time(() =>
            {
                ChartTypes.PieChart(slices, spec.Title ?? "", output,
                    colors: BarChartConfig.DefaultColors, width: 800, height: 600,
                    showLabels: true, showValues: true, showPercent: true);
            });

            WritePngSuccess(command, $"Pie chart saved: {Path.GetFullPath(output)}",
                new { slices = spec.Values.Count, total = Math.Round(total, 2) }, output, elapsed, json);
        }
        catch (JsonException ex)
        {
            CliHelpers.WriteError(command,
                ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {ex.Message}" },
                json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void RenderDiagramFlowchart(string file, string output, bool json)
    {
        const string command = "diagram flowchart";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var elapsed = CliHelpers.Time(() => DiagramBuilder.FromDsl(File.ReadAllText(file), output));
            WritePngSuccess(command, $"Saved: {Path.GetFullPath(output)}", null, output, elapsed, json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void RenderDiagramNetwork(string file, string output, bool json)
    {
        const string command = "diagram network";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var elapsed = CliHelpers.Time(() =>
            {
                var graph = JsonSerializer.Deserialize<Graph>(File.ReadAllText(file),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (graph == null)
                    throw new InvalidOperationException("Network graph spec cannot be empty.");
                DiagramBuilder.NetworkGraph(graph, output);
            });
            WritePngSuccess(command, $"Saved: {Path.GetFullPath(output)}", null, output, elapsed, json);
        }
        catch (JsonException ex)
        {
            CliHelpers.WriteError(command,
                ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {ex.Message}" },
                json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static void RenderDiagramTree(string file, string output, bool json)
    {
        const string command = "diagram tree";
        if (!ValidateWorkerInput(command, file, output, json)) return;

        try
        {
            var elapsed = CliHelpers.Time(() =>
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".json")
                {
                    DiagramBuilder.FromDsl(File.ReadAllText(file), output);
                }
                else
                {
                    var tree = NewickTree.Parse(File.ReadAllText(file).Trim());
                    DiagramBuilder.PhylogeneticTree(tree, output);
                }
            });
            WritePngSuccess(command, $"Saved: {Path.GetFullPath(output)}", null, output, elapsed, json);
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
        }
    }

    static ErrorEntry? ValidateLineSpec(LineSpec? spec)
    {
        if (spec?.Series == null || spec.Series.Count == 0)
            return ErrorCodes.ValidationFailed with { Message = "series array must be non-empty." };

        for (var i = 0; i < spec.Series.Count; i++)
        {
            var series = spec.Series[i];
            if (string.IsNullOrWhiteSpace(series.Name))
                return ErrorCodes.ValidationFailed with { Message = $"Series [{i}]: name is required." };
            if (series.X == null || series.Y == null || series.X.Length != series.Y.Length)
                return ErrorCodes.ValidationFailed with { Message = $"Series '{series.Name}': x and y arrays must have the same length." };
            if (series.X.Length == 0)
                return ErrorCodes.ValidationFailed with { Message = $"Series '{series.Name}': x and y arrays must be non-empty." };
            if (series.X.Any(double.IsNaN) || series.X.Any(double.IsInfinity) ||
                series.Y.Any(double.IsNaN) || series.Y.Any(double.IsInfinity))
                return ErrorCodes.ValidationFailed with { Message = $"Series '{series.Name}': values must not be NaN or Infinity." };
        }

        return null;
    }

    static ErrorEntry? ValidateScatterSpec(ScatterSpec? spec)
    {
        if (spec?.Points == null || spec.Points.Count == 0)
            return ErrorCodes.ValidationFailed with { Message = "points array must be non-empty." };

        if (spec.Points.Any(p => double.IsNaN(p.X) || double.IsInfinity(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.Y)))
            return ErrorCodes.ValidationFailed with { Message = "Point coordinates must not be NaN or Infinity." };

        return null;
    }

    static ErrorEntry? ValidatePieSpec(PieSpec? spec)
    {
        if (spec?.Values == null || spec.Values.Count < 2)
            return ErrorCodes.ValidationFailed with { Message = "values array must have at least 2 entries." };

        foreach (var value in spec.Values)
        {
            if (string.IsNullOrWhiteSpace(value.Label))
                return ErrorCodes.ValidationFailed with { Message = "Each value must have a label." };
            if (value.Value <= 0)
                return ErrorCodes.ValidationFailed with { Message = $"Value '{value.Label}' must be > 0, got {value.Value}." };
        }

        return null;
    }

    static void AddTrendline(Plot plt, IReadOnlyList<ScatterPointEntry> points)
    {
        var allX = points.Select(p => p.X).ToArray();
        var allY = points.Select(p => p.Y).ToArray();
        var sumX = allX.Sum();
        var sumY = allY.Sum();
        var sumXY = allX.Zip(allY, (a, b) => a * b).Sum();
        var sumX2 = allX.Select(a => a * a).Sum();
        var n = allX.Length;
        var denominator = n * sumX2 - sumX * sumX;
        if (Math.Abs(denominator) < double.Epsilon)
            return;

        var slope = (n * sumXY - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / n;
        var xMin = allX.Min();
        var xMax = allX.Max();
        var regLine = plt.Add.Scatter(
            new[] { xMin, xMax },
            new[] { slope * xMin + intercept, slope * xMax + intercept });
        regLine.MarkerSize = 0;
        regLine.LineWidth = 2;
        regLine.LineColor = new Color(200, 50, 50);
        regLine.LegendText = $"y={slope:F3}x+{intercept:F3}";
    }

    static void WritePngSuccess(string command, string summary, object? data, string output, long elapsed, bool json)
    {
        var error = CliHelpers.CheckArtifact(output, "PNG");
        if (error != null)
        {
            CliHelpers.WriteError(command, error, json);
            return;
        }

        if (json)
        {
            var result = JsonOutput.Ok(command, summary, data);
            result.Artifacts["png"] = Path.GetFullPath(output);
            result.Meta.DurationMs = elapsed;
            Console.WriteLine(JsonSerializer.Serialize(result, CliHelpers.JsonOpts));
        }
        else
        {
            Console.WriteLine(summary);
        }
    }
}
