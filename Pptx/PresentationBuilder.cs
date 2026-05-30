using ShapeCrawler;

namespace PptxCore;

public sealed class PresentationBuilder
{
    private readonly IPresentation _pres;
    private readonly List<string> _notes = new();
    private ThemePreset? _theme;
    private bool _showPageNumbers = true;

    internal PresentationBuilder(IPresentation pres) => _pres = pres;

    public PresentationBuilder Widescreen() => this;
    public PresentationBuilder Standard() => this;

    public PresentationBuilder Theme(ThemePreset theme) { _theme = theme; return this; }

    /// <summary>Enable or disable page numbers on content slides</summary>
    public PresentationBuilder PageNumbers(bool show = true)
    {
        _showPageNumbers = show;
        return this;
    }

    public SlideHelper AddSlide()
    {
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];
        // Hide all placeholder text on freeform slides
        foreach (var shape in slide.Shapes)
        {
            if (shape.PlaceholderType is PlaceholderType pt)
            {
                try { shape.TextBox?.SetText(""); } catch { }
            }
        }
        return new SlideHelper(slide) { Theme = _theme };
    }

    public PresentationBuilder AddTitleSlide(Action<TitleSlideBuilder> configure)
    {
        var opt = new TitleSlideBuilder();
        configure(opt);
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];

        // 标题：居中大字
        var title = opt.TitleText ?? "";
        slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Cover.TitleY,
            LayoutSystem.ContentWidth, 80, title);
        var titleShape = slide.Shapes[slide.Shapes.Count - 1];
        StyleTitle(titleShape, LayoutSystem.FontSizes.H1, opt.TitleColorHex);

        // 副标题
        if (!string.IsNullOrEmpty(opt.SubtitleText))
        {
            slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Cover.SubtitleY,
                LayoutSystem.ContentWidth, 40, opt.SubtitleText);
            var subtitleShape = slide.Shapes[slide.Shapes.Count - 1];
            StyleSubtitle(subtitleShape);
        }

        // 作者/日期
        if (!string.IsNullOrEmpty(opt.AuthorText))
        {
            slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Cover.AuthorY,
                LayoutSystem.ContentWidth, 30, opt.AuthorText);
            var authorShape = slide.Shapes[slide.Shapes.Count - 1];
            StyleBody(authorShape, LayoutSystem.FontSizes.Body_SM);
        }

        // 装饰线
        if (_theme != null)
        {
            slide.Shapes.AddShape(LayoutSystem.Margin_X, LayoutSystem.Cover.DecorationY,
                LayoutSystem.Decoration.AccentLineWidth, LayoutSystem.Decoration.AccentLineHeight);
            var line = slide.Shapes[slide.Shapes.Count - 1];
            line.Fill.SetColor(_theme.Accent1);
        }

        return this;
    }

    public PresentationBuilder AddContentSlide(Action<ContentSlideBuilder> configure)
    {
        var opt = new ContentSlideBuilder();
        configure(opt);
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];

        // 标题
        var title = opt.TitleText ?? "";
        slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Content.TitleY,
            LayoutSystem.ContentWidth, 60, title);
        var titleShape = slide.Shapes[slide.Shapes.Count - 1];
        StyleHeading(titleShape, LayoutSystem.FontSizes.H2, null);

        // 装饰线（标题下）
        if (_theme != null)
        {
            slide.Shapes.AddShape(LayoutSystem.Margin_X, LayoutSystem.Content.BodyY - LayoutSystem.Spacing_SM,
                LayoutSystem.Decoration.AccentLineWidth, LayoutSystem.Decoration.AccentLineHeight);
            var line = slide.Shapes[slide.Shapes.Count - 1];
            line.Fill.SetColor(_theme.Accent1);
        }

        // 要点列表
        int y = LayoutSystem.Content.BodyY;
        foreach (var bulletText in opt.BulletItems)
        {
            slide.Shapes.AddTextBox(LayoutSystem.Margin_X + LayoutSystem.Content.BulletIndent,
                y, LayoutSystem.ContentWidth - LayoutSystem.Content.BulletIndent,
                LayoutSystem.Content.BulletLineHeight, bulletText);
            var shape = slide.Shapes[slide.Shapes.Count - 1];
            StyleBullet(shape);
            y += LayoutSystem.Content.BulletLineHeight;
        }

        // 页码
        if (_showPageNumbers)
        {
            AddPageNumber(slide);
        }

        return this;
    }

    public PresentationBuilder AddTableSlide(Action<TableSlideBuilder> configure)
    {
        var opt = new TableSlideBuilder();
        configure(opt);
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];

        // 标题
        var title = opt.TitleText ?? "";
        slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Content.TitleY,
            LayoutSystem.ContentWidth, 60, title);
        var titleShape = slide.Shapes[slide.Shapes.Count - 1];
        StyleHeading(titleShape, LayoutSystem.FontSizes.H2, null);

        // 表格
        if (opt.TableData != null && opt.TableData.Length > 0)
        {
            int rows = opt.TableData.Length;
            int cols = opt.TableData[0].Length;
            int tableWidth = LayoutSystem.ContentWidth;
            int tableHeight = 360;  // 固定高度

            slide.Shapes.AddTable(LayoutSystem.Margin_X, LayoutSystem.Content.BodyY,
                cols, rows);
            var tableShape = slide.Shapes[slide.Shapes.Count - 1];
            var table = tableShape.Table!;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols && c < opt.TableData[r].Length; c++)
                {
                    var textBox = table[r, c].TextBox;
                    if (textBox != null)
                    {
                        textBox.SetText(opt.TableData[r][c]);
                    }
                }
            }

            StyleTable(table, rows, cols);
        }

        // 页码
        if (_showPageNumbers)
        {
            AddPageNumber(slide);
        }

        return this;
    }

    public PresentationBuilder AddChartSlide(Action<ChartSlideBuilder> configure)
    {
        var opt = new ChartSlideBuilder();
        configure(opt);
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];

        // 标题
        var title = opt.TitleText ?? "";
        slide.Shapes.AddTextBox(LayoutSystem.Margin_X, LayoutSystem.Chart.TitleY,
            LayoutSystem.ContentWidth, 60, title);
        var titleShape = slide.Shapes[slide.Shapes.Count - 1];
        StyleHeading(titleShape, LayoutSystem.FontSizes.H2, null);

        // 图表
        if (opt.PieData != null)
        {
            slide.Shapes.AddPieChart(LayoutSystem.Margin_X, LayoutSystem.Chart.ChartY,
                LayoutSystem.Chart.ChartWidth, LayoutSystem.Chart.ChartHeight,
                opt.PieData, "Series 1");
        }
        else if (opt.BarData != null)
        {
            slide.Shapes.AddBarChart(LayoutSystem.Margin_X, LayoutSystem.Chart.ChartY,
                LayoutSystem.Chart.ChartWidth, LayoutSystem.Chart.ChartHeight,
                opt.BarData, "Series 1");
        }

        // 页码
        if (_showPageNumbers)
        {
            AddPageNumber(slide);
        }

        return this;
    }

    public PresentationBuilder Background(string colorHex) => this;

    public PresentationBuilder Notes(params string[] lines) { _notes.AddRange(lines); return this; }

    public string Save(string path)
    {
        if (_pres.Slides.Count == 0) _pres.Slides.Add(1);

        // Inject theme into slide master before saving
        if (_theme != null && _pres.MasterSlides.Any())
            _theme.ApplyToMasterSlide(_pres.MasterSlides[0]);

        if (_notes.Count > 0) _pres.Slides[_pres.Slides.Count - 1].AddNotes(_notes);
        _pres.Save(path);
        return Path.GetFullPath(path);
    }

    public string AsMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Presentation ({_pres.Slides.Count} slides)");
        sb.AppendLine();
        for (int i = 0; i < _pres.Slides.Count; i++)
        {
            var slide = _pres.Slides[i];
            sb.AppendLine($"## Slide {i + 1}");
            foreach (var shape in slide.Shapes)
            {
                if (shape.TextBox != null && !string.IsNullOrWhiteSpace(shape.TextBox.Text))
                    sb.AppendLine($"- {shape.TextBox.Text}");
                if (shape.Table != null)
                {
                    sb.AppendLine("| | |");
                    sb.AppendLine("|---|---|");
                    for (int r = 0; r < shape.Table.Rows.Count; r++)
                    {
                        var cells = new List<string>();
                        for (int c = 0; c < shape.Table.Columns.Count; c++)
                            cells.Add(shape.Table[r, c].TextBox.Text);
                        sb.AppendLine("| " + string.Join(" | ", cells) + " |");
                    }
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ── Placeholder helpers ──

    private static void SetPlaceholder(IUserSlide slide, PlaceholderType type, string text)
    {
        foreach (var shape in slide.Shapes)
        {
            if (shape.PlaceholderType == type)
            {
                shape.TextBox?.SetText(text);
                return;
            }
        }
        slide.Shapes.AddTextBox(40, 30, 880, 60, text);
    }

    private static IShape? FindShape(IUserSlide slide, PlaceholderType type)
    {
        foreach (var shape in slide.Shapes)
            if (shape.PlaceholderType == type)
                return shape;
        return null;
    }

    private static void HideUnusedPlaceholders(IUserSlide slide, HashSet<PlaceholderType> used)
    {
        foreach (var shape in slide.Shapes)
        {
            if (shape.PlaceholderType is PlaceholderType pt && !used.Contains(pt))
            {
                try { shape.TextBox?.SetText(""); } catch { /* best effort */ }
            }
        }
    }

    // ── Theme styling helpers ──

    private void StyleHeading(IShape shape, decimal fontSize, string? colorOverride)
    {
        if (_theme == null) return;
        try
        {
            if (shape.TextBox is not { Paragraphs.Count: > 0 } tb) return;
            var para = tb.Paragraphs[0];
            if (para.Portions.Count == 0) return;
            var font = para.Portions[0].Font;
            font.LatinName = _theme.HeadFont;
            font.EastAsianName = _theme.HeadCJK;
            font.Size = fontSize;
            font.IsBold = true;
            font.Color.Set(!string.IsNullOrEmpty(colorOverride) ? colorOverride : _theme.Accent1);
        }
        catch { /* best effort */ }
    }

    private void StyleSubtitle(IShape shape)
    {
        if (_theme == null) return;
        try
        {
            if (shape.TextBox is not { Paragraphs.Count: > 0 } tb) return;
            var para = tb.Paragraphs[0];
            if (para.Portions.Count == 0) return;
            var font = para.Portions[0].Font;
            font.LatinName = _theme.BodyFont;
            font.EastAsianName = _theme.BodyCJK;
            font.Size = 20m;
            font.Color.Set(_theme.Accent2);
        }
        catch { /* best effort */ }
    }

    private void StyleBody(IShape shape, decimal fontSize)
    {
        if (_theme == null) return;
        try
        {
            if (shape.TextBox is not { Paragraphs.Count: > 0 } tb) return;
            var para = tb.Paragraphs[0];
            if (para.Portions.Count == 0) return;
            var font = para.Portions[0].Font;
            font.LatinName = _theme.BodyFont;
            font.EastAsianName = _theme.BodyCJK;
            font.Size = fontSize;
            font.Color.Set(_theme.Dark1);
        }
        catch { /* best effort */ }
    }

    private void StyleBullet(IShape shape)
    {
        if (_theme == null) return;
        try
        {
            if (shape.TextBox is not { Paragraphs.Count: > 0 } tb) return;
            foreach (var para in tb.Paragraphs)
            {
                para.Bullet.Type = BulletType.Character;
                para.Bullet.Character = "•";
                para.Bullet.Size = 70;
                if (para.Portions.Count > 0)
                {
                    var font = para.Portions[0].Font;
                    font.LatinName = _theme.BodyFont;
                    font.EastAsianName = _theme.BodyCJK;
                    font.Size = 18m;
                    font.Color.Set(_theme.Dark1);
                }
            }
        }
        catch { /* best effort */ }
    }

    private void StyleTable(ITable table, int rows, int cols)
    {
        if (_theme == null) return;
        try
        {
            // Default fill for whole table
            try { table.UpdateFill(_theme.Light1); } catch { }

            // Header row: dark fill, white bold text
            for (int c = 0; c < cols; c++)
            {
                try
                {
                    var cell = table[0, c];
                    cell.Fill.SetColor(_theme.Accent1);
                    if (cell.TextBox is { Paragraphs.Count: > 0 } ctb
                        && ctb.Paragraphs[0].Portions.Count > 0)
                    {
                        var font = ctb.Paragraphs[0].Portions[0].Font;
                        font.LatinName = _theme.HeadFont;
                        font.EastAsianName = _theme.HeadCJK;
                        font.Size = 16m;
                        font.IsBold = true;
                        font.Color.Set(_theme.Light1);
                    }
                }
                catch { /* per-cell best effort */ }
            }

            // Alternating row background for data rows
            for (int r = 1; r < rows; r++)
            {
                if (r % 2 == 1) continue; // skip odd rows (1-based: skip row 1, style row 2, etc.)
                for (int c = 0; c < cols; c++)
                {
                    try { table[r, c].Fill.SetColor(_theme.Light2); } catch { }
                }
            }
        }
        catch { /* best effort */ }
    }

    private void StyleTitle(IShape shape, decimal fontSize, string? colorOverride)
    {
        if (_theme == null) return;
        try
        {
            if (shape.TextBox is not { Paragraphs.Count: > 0 } tb) return;
            var para = tb.Paragraphs[0];
            if (para.Portions.Count == 0) return;
            var font = para.Portions[0].Font;
            font.LatinName = _theme.HeadFont;
            font.EastAsianName = _theme.HeadCJK;
            font.Size = fontSize;
            font.IsBold = true;
            font.Color.Set(!string.IsNullOrEmpty(colorOverride) ? colorOverride : _theme.Accent1);
        }
        catch { /* best effort */ }
    }

    private void AddPageNumber(IUserSlide slide)
    {
        try
        {
            int slideIndex = _pres.Slides.Count;  // 当前刚添加的 slide
            slide.Shapes.AddTextBox(
                LayoutSystem.SlideWidth - LayoutSystem.Margin_X - 50,
                LayoutSystem.SlideHeight - LayoutSystem.Margin_Y - 20,
                50, 20,
                slideIndex.ToString()
            );
            var shape = slide.Shapes[slide.Shapes.Count - 1];
            if (shape.TextBox is { Paragraphs.Count: > 0 } tb
                && tb.Paragraphs[0].Portions.Count > 0)
            {
                var font = tb.Paragraphs[0].Portions[0].Font;
                font.Size = LayoutSystem.FontSizes.Caption;
                font.Color.Set("999999");
                if (_theme != null)
                {
                    font.LatinName = _theme.BodyFont;
                    font.EastAsianName = _theme.BodyCJK;
                }
            }
        }
        catch { /* best effort */ }
    }
}
