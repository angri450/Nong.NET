using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace DocxCore;

/// <summary>
/// Word 文档写入器。每个方法对应论文中一种元素。
/// 用法：new DocumentWriter(body).Heading("引言", 1).Body("正文内容[1]")...
/// </summary>
public class DocumentWriter
{
    readonly Body _body;
    int _h1, _h2;

    public DocumentWriter(Body body) { _body = body; }

    public DocumentWriter Title(string text) { P(text, "Title"); return this; }
    public DocumentWriter SubTitle(string text) { P(text, "SubTitle"); return this; }
    public DocumentWriter AbstractTitle(string text = "摘  要") { P(text, "AbstractTitle"); return this; }
    public DocumentWriter Abstract(string text) { P(text, "Abstract"); return this; }
    public DocumentWriter Keywords(string kw) { var p = NewP("Abstract"); p.Append(RunBold("关键词：")); p.Append(Run(" " + kw)); _body.Append(p); return this; }
    public DocumentWriter BibHeading(string text = "参考文献") { P(text, "BibHeading"); return this; }

    public DocumentWriter Heading(string text, int level = 1) {
        string sid = level switch { 1 => "Heading1", 2 => "Heading2", _ => "Heading3" };
        string prefix = level switch { 1 => $"{++_h1}  ", 2 => $"{_h1}.{++_h2}  ", _ => "" };
        if (level == 1) _h2 = 0;
        P(prefix + text, sid); return this;
    }

    /// <summary>正文段落，自动检测 [N] 引用并上标</summary>
    public DocumentWriter Body(string text) {
        var p = NewP("Normal");
        foreach (Match m in Regex.Matches(text, @"\[\d+(?:[,-]\d+)*\]|."))
            if (m.Value.StartsWith('[')) p.Append(new Run(new RunProperties(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }, new FontSize { Val = "18" }), new Text(m.Value)));
            else p.Append(Run(m.Value));
        _body.Append(p); return this;
    }

    /// <summary>图占位符</summary>
    public DocumentWriter Figure(string caption, int? num = null) {
        int n = num ?? 1;
        var p = NewP("BodyTextNoIndent", JustificationValues.Center);
        p.Append(Run("[在此处插入图片]", "18"));
        _body.Append(p);
        var c = NewP("BodyTextNoIndent", JustificationValues.Center);
        c.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "16" }), new Text($"图{n}  {caption}")));
        _body.Append(c); _body.Append(new Paragraph()); return this;
    }

    /// <summary>三线表</summary>
    public DocumentWriter Table(string caption, int num, string[] headers, string[][] rows) {
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
        var g = new TableGrid(); foreach (var _ in headers) g.Append(new GridColumn()); t.Append(g);

        // 表头行（带下边框 = 细线）
        var hr = new TableRow();
        foreach (var h in headers) hr.Append(MakeCell(h, true, true));
        t.Append(hr);

        // 数据行
        foreach (var r in rows) {
            var tr = new TableRow();
            foreach (var c in r) tr.Append(MakeCell(c, false, false));
            t.Append(tr);
        }
        _body.Append(t); _body.Append(new Paragraph()); return this;
    }

    /// <summary>参考文献列表（Word 自动编号 [1][2]...）</summary>
    public DocumentWriter References(params string[] refs) {
        foreach (var txt in refs) {
            var p = NewP("ReferenceText"); p.Append(Run(txt)); _body.Append(p);
        }
        return this;
    }

    // === helpers ===
    void P(string t, string sid) { var p = NewP(sid); p.Append(Run(t)); _body.Append(p); }
    static Paragraph NewP(string sid, JustificationValues? align = null) {
        var ppr = new ParagraphProperties(new ParagraphStyleId { Val = sid });
        if (align != null) ppr.Justification = new Justification { Val = align.Value };
        return new Paragraph(ppr);
    }
    static Run Run(string t, string? fs = null) => fs != null
        ? new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = fs }), new Text(t))
        : new Run(new Text(t));
    static Run RunBold(string t) => new Run(new RunProperties(new Bold()), new Text(t));

    static TopBorder Top(BorderValues v, uint sz = 6) => new TopBorder { Val = v, Size = sz, Color = "000000" };
    static BottomBorder Bottom(BorderValues v, uint sz = 6) => new BottomBorder { Val = v, Size = sz, Color = "000000" };
    static LeftBorder Left(BorderValues v) => new LeftBorder { Val = v };
    static RightBorder Right(BorderValues v) => new RightBorder { Val = v };
    static InsideHorizontalBorder InsideH(BorderValues v) => new InsideHorizontalBorder { Val = v };
    static InsideVerticalBorder InsideV(BorderValues v) => new InsideVerticalBorder { Val = v };

    static TableCell MakeCell(string tx, bool header, bool bottomBorder) {
        var tc = new TableCell();
        var tcProps = new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        if (bottomBorder) tcProps.Append(new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 4u, Color = "000000" }));
        tc.Append(tcProps);
        var p = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "BodyTextNoIndent" },
            new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { Before = "40", After = "40" }));
        if (header) p.Append(new Run(new RunProperties(new Bold(), new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "黑体" }, new FontSize { Val = "21" }), new Text(tx)));
        else p.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "21" }), new Text(tx)));
        tc.Append(p); return tc;
    }
}
