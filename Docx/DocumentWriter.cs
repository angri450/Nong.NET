using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace DocxCore;

/// <summary>
/// Word 文档写入器。每个方法对应论文中一种元素。
/// 用法：new DocumentWriter(body).Heading("引言", 1).Body("正文内容[1]")...
///
/// 如需脚注/尾注支持，传入 WordprocessingDocument：
/// new DocumentWriter(body, doc).Body("文本").Footnote("脚注内容")
/// </summary>
public class DocumentWriter
{
    readonly Body _body;
    readonly WordprocessingDocument? _doc;
    FootnotesPart? _fnPart;
    EndnotesPart? _enPart;
    int _h1, _h2, _fnId, _enId;

    /// <summary>基础构造（无脚注/尾注支持）。</summary>
    public DocumentWriter(Body body)
    {
        _body = body;
        _doc = null;
    }

    /// <summary>完整构造，支持脚注、尾注、图片等高级功能。</summary>
    public DocumentWriter(Body body, WordprocessingDocument doc)
    {
        _body = body;
        _doc = doc;
    }

    // ===== 中文标题/摘要 =====

    public DocumentWriter Title(string text) { P(text, "Title"); return this; }
    public DocumentWriter SubTitle(string text) { P(text, "SubTitle"); return this; }
    public DocumentWriter AbstractTitle(string text = "摘  要") { P(text, "AbstractTitle"); return this; }
    public DocumentWriter Abstract(string text) { P(text, "Abstract"); return this; }
    public DocumentWriter Keywords(string kw)
    {
        var p = NewP("Abstract");
        p.Append(RunBold("关键词："));
        p.Append(Run(" " + kw));
        _body.Append(p);
        return this;
    }

    // ===== 英文标题/摘要 =====

    /// <summary>英文标题（使用 EnglishTitle 样式）。</summary>
    public DocumentWriter EnglishTitle(string text) { P(text, "EnglishTitle"); return this; }

    /// <summary>英文摘要标题。</summary>
    public DocumentWriter EnglishAbstractTitle(string text = "Abstract") { P(text, "AbstractTitle"); return this; }

    /// <summary>英文摘要正文。</summary>
    public DocumentWriter EnglishAbstract(string text) { P(text, "Abstract"); return this; }

    /// <summary>英文关键词。</summary>
    public DocumentWriter EnglishKeywords(string kw)
    {
        var p = NewP("Abstract");
        p.Append(RunBold("Key words: "));
        p.Append(Run(kw));
        _body.Append(p);
        return this;
    }

    // ===== 标题 =====

    public DocumentWriter Heading(string text, int level = 1)
    {
        string sid = level switch { 1 => "Heading1", 2 => "Heading2", _ => "Heading3" };
        string prefix = level switch { 1 => $"{++_h1}  ", 2 => $"{_h1}.{++_h2}  ", _ => "" };
        if (level == 1) _h2 = 0;
        P(prefix + text, sid);
        return this;
    }

    /// <summary>参考文献标题。</summary>
    public DocumentWriter BibHeading(string text = "参考文献") { P(text, "BibHeading"); return this; }

    // ===== 正文 =====

    /// <summary>正文段落，自动检测 [N] 引用并上标。</summary>
    public DocumentWriter Body(string text)
    {
        var p = NewP("Normal");
        foreach (Match m in Regex.Matches(text, @"\[\d+(?:[,-]\d+)*\]|."))
            if (m.Value.StartsWith('['))
                p.Append(new Run(new RunProperties(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }, new FontSize { Val = "18" }), new Text(m.Value)));
            else
                p.Append(Run(m.Value));
        _body.Append(p);
        return this;
    }

    // ===== 图表 =====

    /// <summary>图占位符。</summary>
    public DocumentWriter Figure(string caption, int? num = null)
    {
        int n = num ?? 1;
        var p = NewP("BodyTextNoIndent", JustificationValues.Center);
        p.Append(Run("[在此处插入图片]", "18"));
        _body.Append(p);
        var c = NewP("BodyTextNoIndent", JustificationValues.Center);
        c.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "16" }), new Text($"图{n}  {caption}")));
        _body.Append(c);
        _body.Append(new Paragraph());
        return this;
    }

    // ===== 表格 =====

    /// <summary>三线表。</summary>
    public DocumentWriter Table(string caption, int num, string[] headers, string[][] rows)
    {
        var cp = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
        cp.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "16" }), new Text($"表{num}  {caption}")));
        _body.Append(cp);

        var t = new Table();
        t.Append(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                Top(BorderValues.Single, 6), Bottom(BorderValues.Single, 6),
                Left(BorderValues.None), Right(BorderValues.None),
                InsideH(BorderValues.None), InsideV(BorderValues.None)),
            new TableLayout { Type = TableLayoutValues.Fixed }));
        var g = new TableGrid();
        foreach (var _ in headers) g.Append(new GridColumn());
        t.Append(g);

        var hr = new TableRow();
        foreach (var h in headers) hr.Append(MakeCell(h, true, true));
        t.Append(hr);

        foreach (var r in rows)
        {
            var tr = new TableRow();
            foreach (var c in r) tr.Append(MakeCell(c, false, false));
            t.Append(tr);
        }
        _body.Append(t);
        _body.Append(new Paragraph());
        return this;
    }

    /// <summary>变量操作化三线表。</summary>
    public DocumentWriter VariableTable(string caption, int num, List<VariablePlanRow> variables)
    {
        var headers = VariablePlanGenerator.Columns;
        var rows = variables.Select(v => new[] {
            v.变量名称, v.中文标签, v.变量角色, v.理论含义, v.操作化方式,
            v.数据类型, v.测量题项指标, v.取值范围, v.数据来源, v.是否必须,
            v.分析用途, v.缺失风险,
        }).ToArray();
        return Table(caption, num, headers, rows);
    }

    // ===== 参考文献 =====

    /// <summary>参考文献列表（Word 自动编号 [1][2]...）。</summary>
    public DocumentWriter References(params string[] refs)
    {
        foreach (var txt in refs)
        {
            var p = NewP("ReferenceText");
            p.Append(Run(txt));
            _body.Append(p);
        }
        return this;
    }

    // ===== 脚注 =====

    /// <summary>添加脚注。需要传入 WordprocessingDocument 构造。脚注编号自动递增。</summary>
    public DocumentWriter Footnote(string text)
    {
        EnsureDoc();
        _fnId++;
        var fnPart = GetFootnotesPart();
        var fn = new Footnote { Id = _fnId };
        fn.Append(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "FootnoteText" }),
            new Run(new RunProperties(
                new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                new FontSize { Val = "18" }),
                new Text($"{_fnId}. {text}"))));
        fnPart.Footnotes!.Append(fn);

        // 在正文中插入脚注引用
        var refRun = new Run(new RunProperties(
            new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
            new FontSize { Val = "18" }),
            new FootnoteReference { Id = _fnId });
        var refPara = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "FootnoteReference" }), refRun);
        _body.Append(refPara);

        return this;
    }

    // ===== 尾注 =====

    /// <summary>添加尾注。需要传入 WordprocessingDocument 构造。尾注编号自动递增。</summary>
    public DocumentWriter Endnote(string text)
    {
        EnsureDoc();
        _enId++;
        var enPart = GetEndnotesPart();
        var en = new Endnote { Id = _enId };
        en.Append(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "EndnoteText" }),
            new Run(new RunProperties(
                new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                new FontSize { Val = "18" }),
                new Text($"{_enId}. {text}"))));
        enPart.Endnotes!.Append(en);

        var refRun = new Run(new RunProperties(
            new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
            new FontSize { Val = "18" }),
            new EndnoteReference { Id = _enId });
        var refPara = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "FootnoteReference" }), refRun);
        _body.Append(refPara);

        return this;
    }

    // ===== 交叉引用 =====

    /// <summary>内部交叉引用（超链接到书签）。如 CrossReference("_Toc001", "见表 1")。</summary>
    public DocumentWriter CrossReference(string bookmarkName, string displayText)
    {
        var hyperlink = new Hyperlink { Anchor = bookmarkName, History = true };
        hyperlink.Append(new Run(new RunProperties(
            new RunStyle { Val = "Hyperlink" },
            new Underline { Val = UnderlineValues.Single }),
            new Text(displayText)));
        _body.Append(new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "BodyTextNoIndent" }), hyperlink));
        return this;
    }

    /// <summary>外部超链接。</summary>
    public DocumentWriter Hyperlink(string url, string displayText)
    {
        // External hyperlinks need a relationship
        if (_doc == null)
        {
            // Fallback: just output the URL as text
            var p = NewP("Normal");
            p.Append(Run($"{displayText} ({url})"));
            _body.Append(p);
            return this;
        }

        var mainPart = _doc.MainDocumentPart!;
        var extRel = mainPart.AddHyperlinkRelationship(new Uri(url), true);
        var hyperlink = new Hyperlink { Id = extRel.Id };
        hyperlink.Append(new Run(new RunProperties(
            new RunStyle { Val = "Hyperlink" },
            new Underline { Val = UnderlineValues.Single },
            new Color { Val = "0563C1" }),
            new Text(displayText)));
        _body.Append(new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "BodyTextNoIndent" }), hyperlink));
        return this;
    }

    // ===== 书签 =====

    /// <summary>在当前位置插入书签，供交叉引用使用。</summary>
    public DocumentWriter Bookmark(string name)
    {
        var id = Math.Abs(name.GetHashCode() % 0x7FFFFFFF).ToString();
        var p = new Paragraph(
            new BookmarkStart { Name = name, Id = id },
            new BookmarkEnd { Id = id });
        _body.Append(p);
        return this;
    }

    // ===== 目录 =====

    /// <summary>插入目录页（含"目录"标题 + TOC 域 + 分页符）。</summary>
    public DocumentWriter TableOfContents(string title = "目录")
    {
        TocAndChartBuilder.AppendTableOfContents(_body, title);
        return this;
    }

    // ===== 图表 =====

    /// <summary>插入柱状图。</summary>
    public DocumentWriter BarChart(string title, string[] categories, double[] values, string seriesName = "系列 1")
    {
        if (_doc == null) throw new InvalidOperationException("Chart requires WordprocessingDocument constructor.");
        TocAndChartBuilder.AppendBarChart(_body, _doc.MainDocumentPart!, title, categories, values, seriesName);
        return this;
    }

    // ===== 表格样式 =====

    /// <summary>为最后一个表格设置 Word 内置样式。</summary>
    public DocumentWriter TableStyle(string styleId)
    {
        var lastTable = _body.Elements<Table>().LastOrDefault();
        if (lastTable != null)
        {
            var tpr = lastTable.Elements<TableProperties>().FirstOrDefault();
            if (tpr == null) { tpr = new TableProperties(); lastTable.InsertAt(tpr, 0); }
            tpr.Append(new TableStyle { Val = styleId });
        }
        return this;
    }

    // === helpers ===

    void EnsureDoc()
    {
        if (_doc == null)
            throw new InvalidOperationException(
                "Footnote/Endnote support requires WordprocessingDocument. Use 'new DocumentWriter(body, doc)' constructor.");
    }

    FootnotesPart GetFootnotesPart()
    {
        if (_fnPart != null) return _fnPart;
        var mainPart = _doc!.MainDocumentPart!;
        _fnPart = mainPart.FootnotesPart ?? mainPart.AddNewPart<FootnotesPart>();
        if (_fnPart.Footnotes == null)
        {
            _fnPart.Footnotes = new Footnotes(
                new Footnote(new Paragraph()) { Id = 0 },
                new Footnote(new Paragraph()) { Id = -1 });
        }
        return _fnPart;
    }

    EndnotesPart GetEndnotesPart()
    {
        if (_enPart != null) return _enPart;
        var mainPart = _doc!.MainDocumentPart!;
        _enPart = mainPart.EndnotesPart ?? mainPart.AddNewPart<EndnotesPart>();
        if (_enPart.Endnotes == null)
        {
            _enPart.Endnotes = new Endnotes(
                new Endnote(new Paragraph()) { Id = 0 },
                new Endnote(new Paragraph()) { Id = -1 });
        }
        return _enPart;
    }

    void P(string t, string sid)
    {
        var p = NewP(sid);
        p.Append(Run(t));
        _body.Append(p);
    }

    static Paragraph NewP(string sid, JustificationValues? align = null)
    {
        var ppr = new ParagraphProperties(new ParagraphStyleId { Val = sid });
        if (align != null) ppr.Justification = new Justification { Val = align.Value };
        return new Paragraph(ppr);
    }

    static Run Run(string t, string? fs = null) => fs != null
        ? new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = fs }), new Text(t))
        : new Run(new Text(t));

    static Run RunBold(string t) => new Run(new RunProperties(new Bold()), new Text(t));

    static TopBorder Top(BorderValues v, uint sz = 6) => new() { Val = v, Size = sz, Color = "000000" };
    static BottomBorder Bottom(BorderValues v, uint sz = 6) => new() { Val = v, Size = sz, Color = "000000" };
    static LeftBorder Left(BorderValues v) => new() { Val = v };
    static RightBorder Right(BorderValues v) => new() { Val = v };
    static InsideHorizontalBorder InsideH(BorderValues v) => new() { Val = v };
    static InsideVerticalBorder InsideV(BorderValues v) => new() { Val = v };

    static TableCell MakeCell(string tx, bool header, bool bottomBorder)
    {
        var tc = new TableCell();
        var tcProps = new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        if (bottomBorder) tcProps.Append(new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 4u, Color = "000000" }));
        tc.Append(tcProps);
        var p = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "BodyTextNoIndent" },
            new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { Before = "40", After = "40" }));
        if (header)
            p.Append(new Run(new RunProperties(new Bold(), new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "黑体" }, new FontSize { Val = "21" }), new Text(tx)));
        else
            p.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "21" }), new Text(tx)));
        tc.Append(p);
        return tc;
    }
}
