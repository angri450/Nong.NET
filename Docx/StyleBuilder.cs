using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.Json;

namespace DocxCore;

/// <summary>
/// 学术论文格式定义。支持硬编码 BuildAll 和从 JSON 加载 BuildFromJson。
/// 半磅(pt) = 2 * 实际磅数：五号=10.5pt=21halfPt, 小五号=9pt=18halfPt
/// </summary>
public static class StyleBuilder
{
    /// <summary>默认硬编码样式（GB/T 7714 标准）。</summary>
    public static void BuildAll(Styles styles)
    {
        S(styles, "Normal", "Normal", "宋体", "Times New Roman", "21", JustificationValues.Both, "420", "240");
        S(styles, "BodyTextNoIndent", "Body Text No Indent", "宋体", "Times New Roman", "21", JustificationValues.Both);
        S(styles, "Title", "Title", "黑体", "Times New Roman", "28", JustificationValues.Center, line: "360", bold: false);
        S(styles, "SubTitle", "Sub Title", "黑体", "Times New Roman", "32", JustificationValues.Center, line: "340");
        S(styles, "Heading1", "heading 1", "黑体", "Times New Roman", "32", JustificationValues.Left, line: "320", bold: true);
        S(styles, "Heading2", "heading 2", "黑体", "Times New Roman", "28", JustificationValues.Left, line: "300", bold: true);
        S(styles, "Heading3", "heading 3", "黑体", "Times New Roman", "24", JustificationValues.Left, line: "280", bold: true);
        S(styles, "AbstractTitle", "Abstract Title", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240", bold: true);
        S(styles, "Abstract", "Abstract", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        S(styles, "BibHeading", "Bib Heading", "宋体", "Times New Roman", "18", JustificationValues.Left, line: "240", bold: true);
        S(styles, "ReferenceText", "Reference Text", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240", numId: 1);
        // 脚注和尾注样式
        S(styles, "FootnoteText", "Footnote Text", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        S(styles, "FootnoteReference", "Footnote Reference", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        S(styles, "EndnoteText", "Endnote Text", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        S(styles, "EnglishTitle", "English Title", "Times New Roman", "Times New Roman", "28", JustificationValues.Center, line: "360", bold: true);
    }

    /// <summary>
    /// 从 JSON 格式文件加载样式。换格式 = 换 JSON。
    /// JSON 格式参见 formats/journal-paper.json 等。
    /// </summary>
    public static void BuildFromJson(Styles styles, string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        BuildFromJson(styles, doc.RootElement);
    }

    /// <summary>
    /// 从已解析的 JSON 加载样式。
    /// </summary>
    public static void BuildFromJson(Styles styles, JsonElement root)
    {
        if (!root.TryGetProperty("styles", out var stylesEl))
            throw new InvalidOperationException("JSON format must contain a 'styles' object.");

        foreach (var prop in stylesEl.EnumerateObject())
        {
            var styleId = prop.Name;
            var def = prop.Value;

            var fontCJK = def.TryGetProperty("fontCJK", out var fc) ? fc.GetString() ?? "宋体" : "宋体";
            var fontLatin = def.TryGetProperty("fontLatin", out var fl) ? fl.GetString() ?? "Times New Roman" : "Times New Roman";
            var fontSize = def.TryGetProperty("fontSize", out var fs) ? fs.GetString() ?? "21" : "21";
            var bold = def.TryGetProperty("bold", out var b) && b.GetBoolean();
            var alignment = ParseAlignment(def);
            var indent = def.TryGetProperty("firstLineIndent", out var fi) ? fi.GetString() : null;
            var lineSpacing = def.TryGetProperty("lineSpacing", out var ls) ? ls.GetString() : null;
            var numId = def.TryGetProperty("numId", out var ni) && ni.TryGetInt32(out var nid) ? nid : 0;

            S(styles, styleId, styleId, fontCJK, fontLatin, fontSize, alignment, indent, lineSpacing, bold, numId);
        }

        // 如果没有定义 Normal，添加默认 Normal
        if (!root.GetProperty("styles").EnumerateObject().Any(p => p.Name == "Normal"))
            S(styles, "Normal", "Normal", "宋体", "Times New Roman", "21", JustificationValues.Both, "420", "240");

        // 添加常用辅助样式（如果 JSON 没定义）
        var defs = new HashSet<string>(root.GetProperty("styles").EnumerateObject().Select(p => p.Name));
        if (!defs.Contains("BodyTextNoIndent"))
            S(styles, "BodyTextNoIndent", "Body Text No Indent", "宋体", "Times New Roman", "21", JustificationValues.Both);
        if (!defs.Contains("FootnoteText"))
            S(styles, "FootnoteText", "Footnote Text", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
        if (!defs.Contains("FootnoteReference"))
            S(styles, "FootnoteReference", "Footnote Reference", "宋体", "Times New Roman", "18", JustificationValues.Both, line: "240");
    }

    static JustificationValues ParseAlignment(JsonElement def)
    {
        if (!def.TryGetProperty("alignment", out var a)) return JustificationValues.Both;
        return a.GetString() switch
        {
            "center" => JustificationValues.Center,
            "left" => JustificationValues.Left,
            "right" => JustificationValues.Right,
            "both" => JustificationValues.Both,
            _ => JustificationValues.Both,
        };
    }

    static void S(Styles ss, string id, string nm, string cjk, string lat, string sz, JustificationValues al, string? indent = null, string? line = null, bool bold = false, int numId = 0)
    {
        var st = new Style { Type = StyleValues.Paragraph, StyleId = id, StyleName = new StyleName { Val = nm } };
        var ppr = new StyleParagraphProperties(new Justification { Val = al });
        if (indent != null) ppr.Indentation = new Indentation { FirstLine = indent };
        if (line != null) ppr.SpacingBetweenLines = new SpacingBetweenLines { Line = line, LineRule = LineSpacingRuleValues.Auto };
        if (numId > 0) ppr.NumberingProperties = new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = numId });
        st.Append(ppr);
        var rpr = new StyleRunProperties(new RunFonts { Ascii = lat, HighAnsi = lat, EastAsia = cjk }, new FontSize { Val = sz });
        if (bold) rpr.Bold = new Bold();
        st.Append(rpr);
        ss.Append(st);
    }

    /// <summary>参考文献编号列表：[1], [2], [3]...</summary>
    public static void BuildNumbering(Numbering num)
    {
        var abs = new AbstractNum(
            new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = NumberFormatValues.Decimal },
                new LevelText { Val = "[%1]" },
                new ParagraphProperties(new Indentation { Left = "420", Hanging = "420" })
            ) { LevelIndex = 0 }
        ) { AbstractNumberId = 1 };
        num.Append(abs);
        num.Append(new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });
    }

    /// <summary>从 JSON 格式文件加载并返回 SectionBuilder（页面布局）。</summary>
    public static SectionBuilder LoadPageLayout(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var sb = new SectionBuilder();
        if (doc.RootElement.TryGetProperty("page", out var page))
            sb.FromJson(page);
        return sb;
    }
}
