using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Export;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

public static class LitCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("lit", "Literature retrieval operations");
        cmd.AddCommand(CreateParse(jsonOpt));
        cmd.AddCommand(CreateValidate(jsonOpt));
        cmd.AddCommand(CreatePlan(jsonOpt));
        cmd.AddCommand(CreateSearch(jsonOpt));
        cmd.AddCommand(CreateExport(jsonOpt));
        cmd.AddCommand(CreateBatch(jsonOpt));
        return cmd;
    }

    static Command CreateParse(Option<bool> jsonOpt)
    {
        var queryOpt = QueryOption();
        var cmd = new Command("parse", "Parse CNKI-like literature retrieval DSL") { queryOpt };
        cmd.SetHandler((string query, bool json) =>
        {
            var (parsed, elapsed) = CliHelpers.Time(() => CnkiParser.Parse(query));
            var output = JsonOutput.Ok("lit parse", $"Parsed {parsed.Terms.Count} term(s)", new
            {
                query = parsed.Text,
                valid = parsed.IsValid,
                fields = CnkiQueryNormalizer.Normalize(parsed).Fields,
                terms = parsed.Terms.Select(t => new
                {
                    field = t.EffectiveField,
                    value = t.Value,
                    phrase = t.IsPhrase,
                    between = t.IsBetween,
                    start = t.BetweenStart,
                    end = t.BetweenEnd
                }),
                issues = parsed.Issues
            });
            output.Metrics["terms"] = parsed.Terms.Count;
            output.Meta.DurationMs = elapsed;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, queryOpt, jsonOpt);
        return cmd;
    }

    static Command CreateValidate(Option<bool> jsonOpt)
    {
        var queryOpt = QueryOption();
        var cmd = new Command("validate", "Validate CNKI-like literature retrieval DSL") { queryOpt };
        cmd.SetHandler((string query, bool json) =>
        {
            var (validation, elapsed) = CliHelpers.Time(() => CnkiDslValidator.Validate(query));
            if (!validation.IsValid)
            {
                var message = string.Join(" ", validation.Issues.Select(i => i.Message));
                CliHelpers.WriteError("lit validate", ErrorCodes.ValidationFailed with { Message = message }, json);
                return;
            }

            var normalized = CnkiQueryNormalizer.Normalize(validation.Query);
            var output = JsonOutput.Ok("lit validate", "Literature DSL is valid", new
            {
                fields = normalized.Fields,
                concepts = normalized.Concepts
            });
            output.Metrics["terms"] = validation.Query.Terms.Count;
            output.Meta.DurationMs = elapsed;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, queryOpt, jsonOpt);
        return cmd;
    }

    static Command CreatePlan(Option<bool> jsonOpt)
    {
        var queryOpt = QueryOption();
        var sourcesOpt = SourcesOption();
        var cmd = new Command("plan", "Plan provider rough queries for literature retrieval") { queryOpt, sourcesOpt };
        cmd.SetHandler((string query, string sources, bool json) =>
        {
            var parsed = CnkiParser.Parse(query);
            var validation = CnkiDslValidator.Validate(parsed);
            if (!validation.IsValid)
            {
                CliHelpers.WriteError("lit plan", ErrorCodes.ValidationFailed with { Message = string.Join(" ", validation.Issues.Select(i => i.Message)) }, json);
                return;
            }

            var (plan, elapsed) = CliHelpers.Time(() => new QueryPlanner().Plan(parsed, ParseSources(sources)));
            if (plan.Issues.Any(i => string.Equals(i.Severity, "Error", StringComparison.OrdinalIgnoreCase)))
            {
                CliHelpers.WriteError("lit plan", ErrorCodes.ValidationFailed with { Message = string.Join(" ", plan.Issues.Select(i => i.Message)) }, json);
                return;
            }

            var output = JsonOutput.Ok("lit plan", $"Planned {plan.Providers.Count} provider(s)", plan);
            AddIssues(output, plan.Issues);
            output.Metrics["providers"] = plan.Providers.Count;
            output.Meta.DurationMs = elapsed;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, queryOpt, sourcesOpt, jsonOpt);
        return cmd;
    }

    static Command CreateSearch(Option<bool> jsonOpt)
    {
        var queryOpt = QueryOption();
        var sourcesOpt = SourcesOption();
        var limitOpt = new Option<int>("--limit", () => 50, "Maximum number of records to return");
        var profileOpt = new Option<string>("--profile", () => "balanced", "Rank profile: balanced, classic, recent");
        var outOpt = new Option<string?>(new[] { "-o", "--out" }, "Optional JSON output file");
        var modeOpt = new Option<string>("--mode", () => "strict", "Filtering mode: strict, recall");
        var cmd = new Command("search", "Search legal metadata/OA literature providers with local filtering")
        {
            queryOpt,
            sourcesOpt,
            limitOpt,
            profileOpt,
            outOpt,
            modeOpt
        };

        cmd.SetHandler((string query, string sources, int limit, string profile, string? outputPath, string mode, bool json) =>
        {
            try
            {
                var parsed = CnkiParser.Parse(query);
                var validation = CnkiDslValidator.Validate(parsed);
                if (!validation.IsValid)
                {
                    CliHelpers.WriteError("lit search", ErrorCodes.ValidationFailed with { Message = string.Join(" ", validation.Issues.Select(i => i.Message)) }, json);
                    return;
                }

                if (!TryParseProfile(profile, out var rankProfile))
                {
                    CliHelpers.WriteError("lit search", ErrorCodes.ValidationFailed with { Message = $"Unknown rank profile: {profile}" }, json);
                    return;
                }

                var request = new LiteratureSearchRequest
                {
                    Query = query,
                    ParsedQuery = parsed,
                    Sources = ParseSources(sources),
                    Limit = limit,
                    Profile = rankProfile,
                    FilterMode = mode
                };

                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var task = new LiteratureSearchPipeline().SearchAsync(request, CancellationToken.None);
                    task.Wait();
                    return task.Result;
                });

                if (result.Issues.Any(i => string.Equals(i.Severity, "Error", StringComparison.OrdinalIgnoreCase)))
                {
                    CliHelpers.WriteError("lit search", ErrorCodes.ValidationFailed with { Message = string.Join(" ", result.Issues.Select(i => i.Message)) }, json);
                    return;
                }

                if (result.Records.Count == 0
                    && request.Sources.Count == 1
                    && string.Equals(request.Sources[0], "unpaywall", StringComparison.OrdinalIgnoreCase)
                    && result.Issues.Any(i => i.Id == "provider_credential_missing"))
                {
                    CliHelpers.WriteError("lit search", ErrorCodes.DependencyMissing with
                    {
                        Message = "Unpaywall requires NONG_LIT_UNPAYWALL_EMAIL or NONG_LIT_MAILTO."
                    }, json);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    JsonLiteratureExporter.Write(outputPath, result);
                    var artifactError = CliHelpers.CheckArtifact(outputPath, "literature JSON");
                    if (artifactError is not null)
                    {
                        CliHelpers.WriteError("lit search", artifactError, json);
                        return;
                    }
                }

                var output = JsonOutput.Ok("lit search", $"Literature search returned {result.Records.Count} record(s)", result);
                AddIssues(output, result.Issues);
                foreach (var pair in result.Metrics)
                    output.Metrics[pair.Key] = pair.Value;
                output.Metrics["records"] = result.Records.Count;
                if (!string.IsNullOrWhiteSpace(outputPath))
                    output.Artifacts["json"] = Path.GetFullPath(outputPath);
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (AggregateException ae) when (ae.InnerException != null)
            {
                CliHelpers.WriteError("lit search", ErrorCodes.InternalError with { Message = ae.InnerException.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("lit search", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, queryOpt, sourcesOpt, limitOpt, profileOpt, outOpt, modeOpt, jsonOpt);
        return cmd;
    }

    static Command CreateExport(Option<bool> jsonOpt)
    {
        var inputOpt = new Option<string>("--input", "Input LiteratureSearchResult JSON or PaperRecord array") { IsRequired = true };
        var formatOpt = new Option<string>("--format", () => "json", "Export format: json, markdown, bibtex");
        var styleOpt = new Option<string>("--style", () => "gbt7714", "Citation style for markdown");
        var outOpt = new Option<string>(new[] { "-o", "--out" }, "Output artifact path") { IsRequired = true };
        var cmd = new Command("export", "Export normalized literature results as JSON, Markdown, or BibTeX")
        {
            inputOpt,
            formatOpt,
            styleOpt,
            outOpt
        };

        cmd.SetHandler((string input, string format, string style, string outputPath, bool json) =>
        {
            if (!File.Exists(input))
            {
                CliHelpers.WriteError("lit export", ErrorCodes.FileNotFound with { Message = $"File not found: {input}" }, json);
                return;
            }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var loaded = JsonLiteratureExporter.ReadResultOrRecords(input);
                    switch (format.ToLowerInvariant())
                    {
                        case "json":
                            JsonLiteratureExporter.Write(outputPath, loaded);
                            break;
                        case "markdown":
                            MarkdownLiteratureExporter.Write(outputPath, loaded.Records, style);
                            break;
                        case "bibtex":
                            BibTeXExporter.Write(outputPath, loaded.Records);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported export format: {format}");
                    }

                    return loaded;
                });

                var artifactError = CliHelpers.CheckArtifact(outputPath, format);
                if (artifactError is not null)
                {
                    CliHelpers.WriteError("lit export", artifactError, json);
                    return;
                }

                var output = JsonOutput.Ok("lit export", $"Exported {result.Records.Count} record(s) as {format}", new
                {
                    records = result.Records.Count,
                    format,
                    style = format.Equals("markdown", StringComparison.OrdinalIgnoreCase) ? style : null
                });
                output.Artifacts[format.ToLowerInvariant()] = Path.GetFullPath(outputPath);
                output.Metrics["records"] = result.Records.Count;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (InvalidOperationException ex)
            {
                CliHelpers.WriteError("lit export", ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (JsonException ex)
            {
                CliHelpers.WriteError("lit export", ErrorCodes.ReadFailed with { Message = $"Invalid literature JSON: {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("lit export", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, inputOpt, formatOpt, styleOpt, outOpt, jsonOpt);
        return cmd;
    }

    static Option<string> QueryOption() => new("--query", "CNKI-like literature query") { IsRequired = true };

    static Option<string> SourcesOption() => new("--sources", () => "openalex,crossref,aminer", "Comma-separated sources: openalex,crossref,aminer,unpaywall");

    static IReadOnlyList<string> ParseSources(string sources)
    {
        return sources
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static bool TryParseProfile(string profile, out RankProfile rankProfile)
    {
        rankProfile = profile.ToLowerInvariant() switch
        {
            "classic" => RankProfile.Classic,
            "recent" => RankProfile.Recent,
            "balanced" => RankProfile.Balanced,
            _ => RankProfile.Balanced
        };
        return profile.Equals("balanced", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("classic", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("recent", StringComparison.OrdinalIgnoreCase);
    }

    static void AddIssues(JsonOutput output, IEnumerable<LiteratureIssue> issues)
    {
        foreach (var issue in issues)
        {
            output.Issues.Add(new Issue
            {
                Id = issue.Id,
                Severity = issue.Severity,
                Message = issue.Message
            });
        }
    }

    // === lit batch ===

    /// <summary>Regex that captures a complete CNKI DSL query starting with SU=</summary>
    static readonly Regex DslRegex = new(
        @"SU\s*=\s*(?<query>\(.+\))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static Command CreateBatch(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("dir", "Directory containing .txt files with DSL queries");
        var outOpt = new Option<string>("-o", "Output markdown report") { IsRequired = true };
        var sourcesOpt = SourcesOption();
        var limitOpt = new Option<int>("--limit", () => 5, "Max records per DSL query");
        var profileOpt = new Option<string>("--profile", () => "balanced", "Rank profile: balanced, classic, recent");

        var cmd = new Command("batch", "Extract DSL queries from all .txt files in a directory and produce a search report")
        {
            dirArg, outOpt, sourcesOpt, limitOpt, profileOpt
        };

        cmd.SetHandler((string dir, string output, string sources, int limit, string profile, bool json) =>
        {
            if (!Directory.Exists(dir))
            {
                CliHelpers.WriteError("lit batch",
                    ErrorCodes.FileNotFound with { Message = $"Directory not found: {dir}" }, json);
                return;
            }

            var txtFiles = Directory.GetFiles(dir, "*.txt", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).Equals("草案.txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList();

            if (txtFiles.Count == 0)
            {
                CliHelpers.WriteError("lit batch",
                    ErrorCodes.ValidationFailed with { Message = $"No .txt files found in: {dir}" }, json);
                return;
            }

            if (!TryParseProfile(profile, out var rankProfile))
            {
                CliHelpers.WriteError("lit batch",
                    ErrorCodes.ValidationFailed with { Message = $"Unknown rank profile: {profile}" }, json);
                return;
            }

            var sourceList = ParseSources(sources);
            var pipeline = new LiteratureSearchPipeline();
            var allResults = new List<(string FileName, string Section, string Dsl, LiteratureSearchResult Result)>();
            var errors = new List<string>();

            foreach (var file in txtFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var lines = File.ReadAllLines(file);
                string? currentSection = null;

                foreach (var line in lines)
                {
                    // Section headers: "1. xxx" or "方向x" etc.
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("方向") || (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.'))
                    {
                        currentSection = trimmed;
                        continue;
                    }

                    var m = DslRegex.Match(trimmed);
                    if (!m.Success) continue;

                    var dsl = m.Groups["query"].Value; // already captures full parenthesized expression
                    var fullQuery = $"SU={dsl}";

                    try
                    {
                        var request = new LiteratureSearchRequest
                        {
                            Query = fullQuery,
                            Sources = sourceList,
                            Limit = limit,
                            Profile = rankProfile,
                            FilterMode = "recall"  // recall mode essential for CJK→EN queries
                        };

                        var result = pipeline.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult();
                        var section = currentSection ?? Path.GetFileNameWithoutExtension(file);
                        allResults.Add((fileName, section, fullQuery, result));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{fileName}: {fullQuery} → {ex.Message}");
                    }
                }
            }

            // Build markdown report
            var md = new System.Text.StringBuilder();
            md.AppendLine("# 文献检索报告");
            md.AppendLine();
            md.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
            md.AppendLine($"检索目录：{Path.GetFullPath(dir)}");
            md.AppendLine($"检索源：{string.Join(", ", sourceList)}");
            md.AppendLine($"结果数/DSL：{limit}");
            md.AppendLine();

            var grouped = allResults.GroupBy(r => r.FileName);

            foreach (var fileGroup in grouped)
            {
                md.AppendLine($"## {fileGroup.Key}");
                md.AppendLine();

                var sectionGroups = fileGroup.GroupBy(r => r.Section);

                foreach (var sec in sectionGroups)
                {
                    md.AppendLine($"### {sec.Key}");
                    md.AppendLine();

                    foreach (var (_, _, dsl, result) in sec)
                    {
                        md.AppendLine($"**检索式**: `{dsl}`");
                        md.AppendLine();
                        md.AppendLine($"候选 {result.Metrics.GetValueOrDefault("candidates", 0)} 篇 → 去重 {result.Metrics.GetValueOrDefault("merged", 0)} 篇 → 返回 {result.Records.Count} 篇");
                        md.AppendLine();

                        if (errors.Any(e => e.Contains(dsl)))
                        {
                            md.AppendLine("**⚠ 检索失败**");
                            md.AppendLine();
                            continue;
                        }

                        if (result.Records.Count == 0)
                        {
                            md.AppendLine("*无匹配文献*");
                            md.AppendLine();
                            continue;
                        }

                        for (int i = 0; i < result.Records.Count; i++)
                        {
                            var r = result.Records[i];
                            var authors = r.Authors.Count > 3
                                ? string.Join(", ", r.Authors.Take(3)) + " 等"
                                : string.Join(", ", r.Authors);
                            var src = r.RetrievedFrom.FirstOrDefault() ?? "?";
                            var year = r.Year?.ToString() ?? "—";
                            var doi = !string.IsNullOrWhiteSpace(r.Doi) ? $" DOI:{r.Doi}" : "";
                            var abs = r.Abstract?.Length > 0
                                ? $"  \n  **摘要**: {TruncateAbstract(r.Abstract, 200)}"
                                : "";

                            md.AppendLine($"{i + 1}. **{EscapeMarkdown(r.Title ?? "无标题")}**");
                            md.AppendLine($"   {authors}. {r.Journal ?? r.Venue ?? "—"}, {year}. [{src}]{doi}{abs}");
                            md.AppendLine();
                        }
                    }
                }
            }

            if (errors.Count > 0)
            {
                md.AppendLine("## 错误");
                md.AppendLine();
                foreach (var e in errors)
                    md.AppendLine($"- {e}");
                md.AppendLine();
            }

            md.AppendLine("---");
            md.AppendLine($"报告由 Nong.Cli.Net {CliVersion.Current} 生成");

            File.WriteAllText(output, md.ToString(), System.Text.Encoding.UTF8);

            var totalQueries = allResults.Count + errors.Count(e => !allResults.Any(r => e.Contains(r.Dsl)));
            var okQueries = allResults.Count(r => r.Result.Records.Count > 0);

            if (json)
            {
                var outputJson = JsonOutput.Ok("lit batch",
                    $"Batch search: {okQueries}/{totalQueries} DSL queries produced results", new
                    {
                        dir = Path.GetFullPath(dir),
                        files = txtFiles.Select(Path.GetFileNameWithoutExtension),
                        totalQueries,
                        successfulQueries = okQueries,
                        totalRecords = allResults.Sum(r => r.Result.Records.Count),
                        errors = errors.Count
                    });
                outputJson.Artifacts["report"] = Path.GetFullPath(output);
                Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"Batch search complete: {okQueries}/{totalQueries} DSL(s) → {Path.GetFullPath(output)}");
            }
        }, dirArg, outOpt, sourcesOpt, limitOpt, profileOpt, jsonOpt);

        return cmd;
    }

    static string TruncateAbstract(string text, int maxLen)
    {
        if (text.Length <= maxLen) return EscapeMarkdown(text);
        // Cut at word boundary
        var cut = text.LastIndexOf(' ', maxLen);
        if (cut < maxLen / 2) cut = maxLen;
        return EscapeMarkdown(text[..cut]) + "…";
    }

    static string EscapeMarkdown(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("`", "\\`")
            .Replace("[", "\\[")
            .Replace("]", "\\]");
    }
}
