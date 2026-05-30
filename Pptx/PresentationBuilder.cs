using ShapeCrawler;

namespace PptxCore;

public sealed class PresentationBuilder
{
    private readonly IPresentation _pres;
    private readonly List<string> _notes = new();
    private ThemePreset? _theme;

    internal PresentationBuilder(IPresentation pres) => _pres = pres;

    public PresentationBuilder Widescreen() => this;
    public PresentationBuilder Standard() => this;

    public PresentationBuilder Theme(ThemePreset theme) { _theme = theme; return this; }

    public SlideHelper AddSlide()
    {
        _pres.Slides.Add(1);
        return new SlideHelper(_pres.Slides[_pres.Slides.Count - 1]) { Theme = _theme };
    }

    public PresentationBuilder AddTitleSlide(Action<TitleSlideBuilder> configure)
    {
        var opt = new TitleSlideBuilder();
        configure(opt);
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];
        var hidden = new HashSet<PlaceholderType>();

        if (!string.IsNullOrEmpty(opt.TitleText))
        {
            SetPlaceholder(slide, PlaceholderType.Title, opt.TitleText);
            hidden.Add(PlaceholderType.Title);
            var titleShape = FindShape(slide, PlaceholderType.Title);
            if (titleShape != null)
            {
                var titleSize = opt.TitleFontSize > 0 ? (decimal)opt.TitleFontSize : 36m;
                var titleColor = !string.IsNullOrEmpty(opt.TitleColorHex) ? opt.TitleColorHex : null;
                StyleHeading(titleShape, titleSize, titleColor);
            }
        }
        if (!string.IsNullOrEmpty(opt.SubtitleText))
        {
            SetPlaceholder(slide, PlaceholderType.SubTitle, opt.SubtitleText);
            hidden.Add(PlaceholderType.SubTitle);
            var subShape = FindShape(slide, PlaceholderType.SubTitle);
            if (subShape != null) StyleSubtitle(subShape);
        }

        HideUnusedPlaceholders(slide, hidden);
        return this;
    }

    public PresentationBuilder AddContentSlide(Action<ContentSlideBuilder> configure)
    {
        var opt = new ContentSlideBuilder();
        configure(opt);
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];
        var hidden = new HashSet<PlaceholderType>();

        if (!string.IsNullOrEmpty(opt.TitleText))
        {
            SetPlaceholder(slide, PlaceholderType.Title, opt.TitleText);
            hidden.Add(PlaceholderType.Title);
            var titleShape = FindShape(slide, PlaceholderType.Title);
            if (titleShape != null) StyleHeading(titleShape, 28m, null);
        }

        HideUnusedPlaceholders(slide, hidden);

        int y = 120;
        foreach (var bulletText in opt.BulletItems)
        {
            slide.Shapes.AddTextBox(60, y, 860, 36, bulletText);
            var shape = slide.Shapes[slide.Shapes.Count - 1];
            StyleBullet(shape);
            y += 38;
        }

        return this;
    }

    public PresentationBuilder AddTableSlide(Action<TableSlideBuilder> configure)
    {
        var opt = new TableSlideBuilder();
        configure(opt);
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];
        var hidden = new HashSet<PlaceholderType>();

        if (!string.IsNullOrEmpty(opt.TitleText))
        {
            SetPlaceholder(slide, PlaceholderType.Title, opt.TitleText);
            hidden.Add(PlaceholderType.Title);
            var titleShape = FindShape(slide, PlaceholderType.Title);
            if (titleShape != null) StyleHeading(titleShape, 28m, null);
        }

        HideUnusedPlaceholders(slide, hidden);

        if (opt.TableData is { Length: > 0 })
        {
            int rows = opt.TableData.Length;
            int cols = opt.TableData.Max(r => r.Length);
            slide.Shapes.AddTable(50, 120, cols, rows);
            var tableShape = slide.Shapes[slide.Shapes.Count - 1];
            var table = tableShape.Table!;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols && c < opt.TableData[r].Length; c++)
                    table[r, c].TextBox.SetText(opt.TableData[r][c]);

            StyleTable(table, rows, cols);
        }

        return this;
    }

    public PresentationBuilder AddChartSlide(Action<ChartSlideBuilder> configure)
    {
        var opt = new ChartSlideBuilder();
        configure(opt);
        _pres.Slides.Add(1);
        var slide = _pres.Slides[_pres.Slides.Count - 1];
        var hidden = new HashSet<PlaceholderType>();

        if (!string.IsNullOrEmpty(opt.TitleText))
        {
            SetPlaceholder(slide, PlaceholderType.Title, opt.TitleText);
            hidden.Add(PlaceholderType.Title);
            var titleShape = FindShape(slide, PlaceholderType.Title);
            if (titleShape != null) StyleHeading(titleShape, 28m, null);
        }

        HideUnusedPlaceholders(slide, hidden);

        if (opt.PieData is { Count: > 0 })
            slide.Shapes.AddPieChart(80, 120, 800, 380, opt.PieData, "Series 1");
        else if (opt.BarData is { Count: > 0 })
            slide.Shapes.AddBarChart(80, 120, 800, 380, opt.BarData,
                string.IsNullOrEmpty(opt.BarSeriesName) ? "Series 1" : opt.BarSeriesName);

        if (!string.IsNullOrEmpty(opt.ChartText))
        {
            slide.Shapes.AddTextBox(80, 490, 800, 30, opt.ChartText);
            var chartLabel = slide.Shapes[slide.Shapes.Count - 1];
            StyleBody(chartLabel, 14m);
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
}
