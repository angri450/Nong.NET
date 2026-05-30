using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>段落构建器。链式调用设置段落属性和文本。</summary>
public class ParagraphBuilder
{
    readonly Paragraph _p = new();
    ParagraphProperties? _ppr;
    readonly List<Run> _runs = new();

    public ParagraphBuilder Style(string id) { Ppr().ParagraphStyleId = new ParagraphStyleId { Val = id }; return this; }
    public ParagraphBuilder Align(JustificationValues v) { Ppr().Justification = new Justification { Val = v }; return this; }
    public ParagraphBuilder FirstLineIndent(string val) { Ppr().Indentation = new Indentation { FirstLine = val }; return this; }
    public ParagraphBuilder LineSpacing(string val, LineSpacingRuleValues? rule = null) { Ppr().SpacingBetweenLines = new SpacingBetweenLines { Line = val, LineRule = rule ?? LineSpacingRuleValues.Auto }; return this; }
    public ParagraphBuilder SpaceBefore(string val) { (Ppr().SpacingBetweenLines ??= new SpacingBetweenLines()).Before = val; return this; }
    public ParagraphBuilder SpaceAfter(string val) { (Ppr().SpacingBetweenLines ??= new SpacingBetweenLines()).After = val; return this; }
    public ParagraphBuilder KeepNext() { Ppr().KeepNext = new KeepNext(); return this; }
    public ParagraphBuilder PageBreakBefore() { Ppr().PageBreakBefore = new PageBreakBefore(); return this; }

    /// <summary>添加纯文本 Run。</summary>
    public ParagraphBuilder Text(string t)
        { _runs.Add(new Run(new Text(t))); return this; }

    /// <summary>添加带格式的 Run。fontSize 用 half-point（五号=21）。</summary>
    public ParagraphBuilder Run(string t, Action<RunProperties>? config = null)
    {
        var rpr = new RunProperties();
        config?.Invoke(rpr);
        _runs.Add(new Run(rpr.HasChildren ? rpr : null!, new Text(t)));
        return this;
    }

    /// <summary>上标 Run（用于引文序号）。</summary>
    public ParagraphBuilder Sup(string t)
        => Run(t, r => { r.VerticalTextAlignment = new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }; r.FontSize = new FontSize { Val = "18" }; });

    /// <summary>加粗 Run。</summary>
    public ParagraphBuilder Bold(string t)
        => Run(t, r => { r.Bold = new Bold(); });

    public Paragraph Build()
    {
        if (_ppr != null) _p.Append(_ppr);
        foreach (var r in _runs) _p.Append(r);
        return _p;
    }

    ParagraphProperties Ppr() { _ppr ??= new ParagraphProperties(); return _ppr; }
}

/// <summary>表格构建器。</summary>
public class TableBuilder
{
    readonly Table _t = new();
    readonly TableProperties _tpr = new();
    readonly TableGrid _grid = new();
    readonly List<TableRow> _rows = new();

    public TableBuilder WidthPct(int pct = 100) { _tpr.Append(new TableWidth { Type = TableWidthUnitValues.Pct, Width = (pct * 50).ToString() }); return this; }
    public TableBuilder AutoWidth() { _tpr.Append(new TableLayout { Type = TableLayoutValues.Fixed }); return this; }

    /// <summary>三线表边框。thick=顶底线粗细(0.75pt=6)，thin=表头下线(0.5pt=4)。</summary>
    public TableBuilder ThreeLineBorders(uint thick = 6, uint thin = 4)
    {
        _tpr.Append(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = thick, Color = "000000" },
            new BottomBorder { Val = BorderValues.Single, Size = thick, Color = "000000" },
            new LeftBorder { Val = BorderValues.None },
            new RightBorder { Val = BorderValues.None },
            new InsideHorizontalBorder { Val = BorderValues.None },
            new InsideVerticalBorder { Val = BorderValues.None }
        ));
        return this;
    }

    public TableBuilder Columns(params int[] widths)
    {
        foreach (var w in widths) _grid.Append(new GridColumn { Width = w.ToString() });
        return this;
    }

    /// <summary>表头行：黑体加粗，每格下边框=细线。</summary>
    public TableBuilder HeaderRow(params string[] cells)
    {
        var row = new TableRow();
        foreach (var c in cells)
            row.Append(MakeCell(c, isHeader: true, bottomBorder: true));
        _rows.Add(row);
        return this;
    }

    /// <summary>数据行。</summary>
    public TableBuilder DataRow(params string[] cells)
    {
        var row = new TableRow();
        foreach (var c in cells)
            row.Append(MakeCell(c, isHeader: false, bottomBorder: false));
        _rows.Add(row);
        return this;
    }

    public Table Build()
    {
        _t.Append(_tpr);
        _t.Append(_grid);
        foreach (var r in _rows) _t.Append(r);
        return _t;
    }

    static TableCell MakeCell(string tx, bool isHeader, bool bottomBorder)
    {
        var tc = new TableCell();
        var tcProps = new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        if (bottomBorder) tcProps.Append(new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 4u, Color = "000000" }));
        tc.Append(tcProps);

        var p = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "BodyTextNoIndent" },
            new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { Before = "40", After = "40" }));

        if (isHeader)
            p.Append(new Run(new RunProperties(new Bold(), new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "黑体" }, new FontSize { Val = "21" }), new Text(tx)));
        else
            p.Append(new Run(new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new FontSize { Val = "21" }), new Text(tx)));
        tc.Append(p);
        return tc;
    }
}

/// <summary>页眉页脚构建器。</summary>
public class HeaderFooterBuilder
{
    readonly HeaderPart _header;
    readonly FooterPart _footer;
    readonly MainDocumentPart _main;

    public HeaderFooterBuilder(WordprocessingDocument doc)
    {
        _main = doc.MainDocumentPart!;
        _header = _main.AddNewPart<HeaderPart>();
        _footer = _main.AddNewPart<FooterPart>();
    }

    public HeaderFooterBuilder PageNumberFooter(string fontCJK = "宋体", string fontSize = "21")
    {
        _footer.Footer = new Footer();
        var p = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
        p.Append(new Run(new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = fontCJK },
            new FontSize { Val = fontSize }
        ), new FieldChar { FieldCharType = FieldCharValues.Begin }));
        p.Append(new Run(new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = fontCJK },
            new FontSize { Val = fontSize }
        ), new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }));
        p.Append(new Run(new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = fontCJK },
            new FontSize { Val = fontSize }
        ), new FieldChar { FieldCharType = FieldCharValues.End }));
        _footer.Footer.Append(p);
        return this;
    }

    public HeaderFooterBuilder SetForSection(SectionProperties sectPr)
    {
        string hId = _main.GetIdOfPart(_header);
        string fId = _main.GetIdOfPart(_footer);
        sectPr.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = hId });
        sectPr.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = fId });
        return this;
    }
}
