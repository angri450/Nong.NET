using System.CommandLine;
using System.Text.Json;
using Nong.Cli.Common;
using Nong.Inspect;

namespace Nong.Cli.Commands;

/// <summary>
/// Inspect command group: paper diagnose and refs check (phase 3), others stubbed.
/// </summary>
public static class InspectCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("inspect", "Content inspection and document writing");

        cmd.AddCommand(CreateDiagnose(jsonOpt));
        cmd.AddCommand(CreateRefsCheck(jsonOpt));
        cmd.AddCommand(CreateWritePaper(jsonOpt));

        var stubs = new (string name, string desc)[]
        {
            ("classify", "Classify paper type (16 types)"),
            ("structure", "Extract paper structure"),
            ("varplan", "Variable operationalization plan"),
            ("evidence", "Evidence chain diagnosis"),
            ("data-req", "Data requirements diagnosis"),
            ("gap", "Gap grade assessment"),
            ("semantics", "Semantic diagnosis"),
        };
        foreach (var (n, d) in stubs)
        {
            var c = new Command(n, d);
            CliHelpers.SetNotImplemented(c, d, jsonOpt);
            cmd.AddCommand(c);
        }

        return cmd;
    }

    // ===== paper diagnose =====

    static Command CreateDiagnose(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("diagnose", "Full paper quality diagnosis pipeline") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null)
            {
                CliHelpers.WriteError("paper diagnose", err, json);
                return;
            }

            var (result, elapsed) = CliHelpers.Time(() => RunDiagnosis(file));

            if (json)
            {
                var data = new
                {
                    paperType = result.PaperType,
                    typeMatch = result.TypeMatchPercent,
                    recommendedData = result.RecommendedData,
                    recommendedMethods = result.RecommendedMethods,
                    structure = new
                    {
                        title = result.Structure?.Title ?? "",
                        keywords = result.Structure?.Keywords ?? new List<string>(),
                        sectionCount = result.Structure?.Sections?.Count ?? 0,
                        hasReferences = result.Structure?.ReferenceStartLine != null,
                        hasAppendix = result.Structure?.AppendixStartLine != null
                    },
                    evidence = result.Evidence.Select(e => new
                    {
                        item = e.诊断项目, adequate = e.是否充分 == "是",
                        issue = e.主要问题, suggestion = e.修改建议, priority = e.优先级
                    }),
                    dataRequirements = result.DataReqs.Select(d => new
                    {
                        item = d.项目, adequate = d.是否充分 == "是",
                        gap = d.缺口说明, requirement = d.最低补充要求
                    }),
                    gapGrade = result.GapGrade.等级,
                    gapDescription = result.GapGrade.判断标准,
                    canContinue = result.GapGrade.是否可继续分析,
                    quality = result.QualityIssues.Select(q => new
                    {
                        category = q.类别, issue = q.具体问题,
                        fixRequirement = q.最低修改要求, priority = q.优先级
                    }),
                    references = new
                    {
                        count = result.ReferenceCount,
                        riskCount = result.ReferenceRisks.Count,
                        risks = result.ReferenceRisks.Select(r => new
                        {
                            problem = r.文献问题, description = r.风险说明, fix = r.修改建议
                        })
                    }
                };

                var metrics = new Dictionary<string, object>
                {
                    ["textLength"] = result.TextLength,
                    ["paperTypeMatch"] = result.TypeMatchPercent,
                    ["evidenceAdequate"] = result.Evidence.Count(e => e.是否充分 == "是"),
                    ["evidenceTotal"] = result.Evidence.Count,
                    ["dataReqsAdequate"] = result.DataReqs.Count(d => d.是否充分 == "是"),
                    ["dataReqsTotal"] = result.DataReqs.Count,
                    ["gapLevel"] = result.GapGrade.等级,
                    ["referenceCount"] = result.ReferenceCount,
                    ["referenceRiskCount"] = result.ReferenceRisks.Count,
                    ["qualityIssueCount"] = result.QualityIssues.Count
                };

                var output = JsonOutput.Ok("paper diagnose",
                    $"Type: {result.PaperType} ({result.TypeMatchPercent}%), Gap: {result.GapGrade.等级}, Evidences: {result.Evidence.Count(e => e.是否充分 == "是")}/{result.Evidence.Count}",
                    data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== Paper Diagnosis ===");
                Console.WriteLine($"Type: {result.PaperType} (match: {result.TypeMatchPercent}%)");
                Console.WriteLine($"Recommended data: {result.RecommendedData}");
                Console.WriteLine($"Recommended methods: {result.RecommendedMethods}");
                Console.WriteLine();

                Console.WriteLine($"--- Evidence Chain ({result.Evidence.Count(e => e.是否充分 == "是")}/{result.Evidence.Count} adequate) ---");
                foreach (var e in result.Evidence)
                {
                    var status = e.是否充分 == "是" ? "[OK]" : "[!!]";
                    Console.WriteLine($"{status} {e.诊断项目}: {e.修改建议}");
                }
                Console.WriteLine();

                Console.WriteLine($"--- Data Requirements ({result.DataReqs.Count(d => d.是否充分 == "是")}/{result.DataReqs.Count} adequate) ---");
                foreach (var d in result.DataReqs)
                {
                    var status = d.是否充分 == "是" ? "[OK]" : "[!!]";
                    Console.WriteLine($"{status} {d.项目}: {d.最低补充要求}");
                }
                Console.WriteLine();

                Console.WriteLine($"--- Gap Grade: {result.GapGrade.等级} ---");
                Console.WriteLine(result.GapGrade.判断标准);
                Console.WriteLine($"Can continue: {result.GapGrade.是否可继续分析}");
                Console.WriteLine();

                if (result.QualityIssues.Count > 0)
                {
                    Console.WriteLine($"--- Quality Issues ({result.QualityIssues.Count}) ---");
                    foreach (var q in result.QualityIssues)
                        Console.WriteLine($"[{q.类别}] {q.具体问题} → {q.最低修改要求}");
                    Console.WriteLine();
                }

                if (result.ReferenceRisks.Count > 0)
                {
                    Console.WriteLine($"--- Reference Risks ({result.ReferenceRisks.Count}) ---");
                    foreach (var r in result.ReferenceRisks)
                        Console.WriteLine($"  [{r.文献问题}] {r.修改建议}");
                }
            }


        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== refs check =====

    static Command CreateRefsCheck(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("refs", "Reference analysis and risk check") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null)
            {
                CliHelpers.WriteError("refs check", err, json);
                return;
            }

            var text = File.ReadAllText(file);
            var (result, elapsed) = CliHelpers.Time(() =>
            {
                var refs = ReferenceAnalyzer.ExtractReferences(text);
                var risks = ReferenceAnalyzer.CheckReferenceRisks(text, refs);
                var strategy = ReferenceAnalyzer.BuildLiteratureSearchStrategy(
                    PaperStructureExtractor.BuildPaperStructure(text).Keywords,
                    PaperTypeClassifier.TopType(text));
                return (refs, risks, strategy);
            });

            if (json)
            {
                var data = new
                {
                    count = result.refs.Count,
                    entries = result.refs.Select(r => new
                    {
                        number = r.序号, raw = r.原文,
                        year = r.年份, formatRisk = r.格式风险
                    }),
                    risks = result.risks.Select(r => new
                    {
                        problem = r.文献问题, location = r.位置,
                        detail = r.风险说明, fix = r.修改建议
                    }),
                    searchStrategy = result.strategy
                };

                var output = JsonOutput.Ok("refs check",
                    $"{result.refs.Count} references, {result.risks.Count} risks",
                    data);
                output.Metrics["referenceCount"] = result.refs.Count;
                output.Metrics["riskCount"] = result.risks.Count;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== References ({result.refs.Count}) ===");
                foreach (var r in result.refs)
                {
                    var risk = string.IsNullOrEmpty(r.格式风险) ? "" : $" [RISK: {r.格式风险}]";
                    Console.WriteLine($"[{r.序号}] {r.原文}{risk}");
                }

                if (result.risks.Count > 0)
                {
                    Console.WriteLine($"\n=== Risks ({result.risks.Count}) ===");
                    foreach (var r in result.risks)
                        Console.WriteLine($"  [{r.文献问题}] {r.修改建议}");
                }

                Console.WriteLine($"\n=== Search Strategy ===");
                Console.WriteLine(result.strategy);
            }


        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== helpers =====

    record DiagnosisResult(
        string PaperType, int TypeMatchPercent, string RecommendedData, string RecommendedMethods,
        PaperStructure? Structure,
        List<EvidenceChainItem> Evidence, List<DataRequirementItem> DataReqs,
        GapGrade GapGrade, List<QualityIssue> QualityIssues,
        int ReferenceCount, List<ReferenceRisk> ReferenceRisks,
        int TextLength
    );

    static DiagnosisResult RunDiagnosis(string filePath)
    {
        var text = File.ReadAllText(filePath);
        var types = PaperTypeClassifier.Classify(text);
        var topType = types[0];
        var structure = PaperStructureExtractor.BuildPaperStructure(text);
        var evidence = PaperDiagnostics.DiagnoseEvidenceChain(text, structure.Sections);
        var dataReqs = PaperDiagnostics.DiagnoseDataRequirements(text);
        var gap = PaperDiagnostics.DiagnoseGapGrade(evidence, dataReqs, types);
        var quality = PaperDiagnostics.DiagnosePaperQuality(text, evidence, dataReqs, gap, structure.Sections);
        var refs = ReferenceAnalyzer.ExtractReferences(text);
        var refRisks = ReferenceAnalyzer.CheckReferenceRisks(text, refs);

        return new DiagnosisResult(
            topType.论文类型, topType.当前匹配度,
            topType.推荐数据, topType.推荐方法,
            structure,
            evidence, dataReqs,
            gap, quality.问题表,
            refs.Count, refRisks,
            text.Length
        );
    }

    // ===== inspect write paper (phase 6) =====

    static Command CreateWritePaper(Option<bool> jsonOpt)
    {
        var specArg = new Argument<string>("spec", "Path to paper spec JSON");
        var outOpt = new Option<string>("-o", "Output docx path") { IsRequired = true };
        var cmd = new Command("write-paper", "Generate paper docx from JSON spec") { specArg, outOpt };

        cmd.SetHandler((string spec, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(spec);
            if (err != null) { CliHelpers.WriteError("inspect write-paper", err, json); return; }

            try
            {
                var elapsed = CliHelpers.Time(() =>
                {
                    var specJson = File.ReadAllText(spec);
                    using var docEl = JsonDocument.Parse(specJson);
                    var root = docEl.RootElement;

                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(output,
                        DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
                    var main = doc.AddMainDocumentPart();
                    main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                        new DocumentFormat.OpenXml.Wordprocessing.Body());
                    var body = main.Document.Body!;

                    var sp = main.AddNewPart<DocumentFormat.OpenXml.Packaging.StyleDefinitionsPart>();
                    sp.Styles = new DocumentFormat.OpenXml.Wordprocessing.Styles();
                    Gbt7714Style.BuildAll(sp.Styles);

                    var np = main.AddNewPart<DocumentFormat.OpenXml.Packaging.NumberingDefinitionsPart>();
                    np.Numbering = new DocumentFormat.OpenXml.Wordprocessing.Numbering();
                    Gbt7714Style.BuildNumbering(np.Numbering);

                    var w = new PaperWriter(body, doc);
                    if (root.TryGetProperty("title", out var t)) w.Title(t.GetString()!);
                    if (root.TryGetProperty("abstract", out var a)) { w.AbstractTitle(); w.Abstract(a.GetString()!); }
                    if (root.TryGetProperty("keywords", out var kw)) w.Keywords(kw.GetString()!);

                    if (root.TryGetProperty("sections", out var secs))
                    {
                        foreach (var sec in secs.EnumerateArray())
                        {
                            var heading = sec.GetProperty("heading").GetString()!;
                            var level = sec.TryGetProperty("level", out var l) ? l.GetInt32() : 1;
                            w.Heading(heading, level);
                            if (sec.TryGetProperty("body", out var bodyArr))
                            {
                                foreach (var bp in bodyArr.EnumerateArray())
                                    w.Body(bp.GetString()!);
                            }
                        }
                    }

                    if (root.TryGetProperty("references", out var refs))
                    {
                        w.BibHeading();
                        w.References(refs.EnumerateArray().Select(r => r.GetString()!).ToArray());
                    }

                    main.Document.Save();
                });

                if (json)
                {
                    var outputJson = JsonOutput.Ok("inspect write-paper", $"Paper saved: {output}");
                    outputJson.Artifacts["docx"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: {Path.GetFullPath(output)}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("inspect write-paper",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }


        }, specArg, outOpt, jsonOpt);

        return cmd;
    }
}
