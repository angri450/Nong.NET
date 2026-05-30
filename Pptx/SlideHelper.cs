using ShapeCrawler;

namespace PptxCore;

public sealed class SlideHelper
{
    private readonly IUserSlide _slide;
    internal ThemePreset? Theme { get; set; }

    internal SlideHelper(IUserSlide slide) => _slide = slide;

    public IUserSlide Slide => _slide;

    public SlideHelper TextBox(string text, int x, int y, int w, int h,
        int fontSize = 18, string fontName = "", bool bold = false,
        string colorHex = "", string align = "Left")
    {
        _slide.Shapes.AddTextBox(x, y, w, h, text);
        var shape = _slide.Shapes[_slide.Shapes.Count - 1];
        if (shape.TextBox is { Paragraphs.Count: > 0 } tb
            && tb.Paragraphs[0].Portions.Count > 0)
        {
            var font = tb.Paragraphs[0].Portions[0].Font;
            if (Theme != null)
            {
                font.LatinName = fontName.Length > 0 ? fontName : Theme.BodyFont;
                font.EastAsianName = Theme.BodyCJK;
                font.Size = fontSize;
                font.IsBold = bold;
                font.Color.Set(colorHex.Length > 0 ? colorHex : Theme.Dark1);
            }
            else
            {
                font.Size = fontSize;
                font.IsBold = bold;
                if (fontName.Length > 0) font.LatinName = fontName;
            }
        }
        return this;
    }

    public SlideHelper Shape(Geometry geometry, int x, int y, int w, int h,
        string fillHex = "", string text = "")
    {
        var scGeom = MapGeometry(geometry);
        if (!string.IsNullOrEmpty(text))
            _slide.Shapes.AddShape(x, y, w, h, scGeom, text);
        else
            _slide.Shapes.AddShape(x, y, w, h, scGeom);
        var shape = _slide.Shapes[_slide.Shapes.Count - 1];
        if (!string.IsNullOrEmpty(fillHex))
        {
            try { shape.Fill.SetColor(fillHex); } catch { }
        }
        else if (Theme != null)
        {
            try { shape.Fill.SetColor(Theme.Accent1); } catch { }
        }
        if (!string.IsNullOrEmpty(text) && Theme != null)
        {
            try { Theme.StyleBody(shape, 14m); } catch { }
        }
        return this;
    }

    public SlideHelper Picture(string imagePath, int? x = null, int? y = null,
        int? w = null, int? h = null)
    {
        using var stream = File.OpenRead(imagePath);
        _slide.Shapes.AddPicture(stream);
        return this;
    }

    public SlideHelper Background(string colorHex)
    {
        try { _slide.Fill.SetColor(colorHex); } catch { }
        return this;
    }

    private static ShapeCrawler.Geometry MapGeometry(PptxCore.Geometry geo) => geo switch
    {
        PptxCore.Geometry.Rectangle => ShapeCrawler.Geometry.Rectangle,
        PptxCore.Geometry.RoundedRectangle => ShapeCrawler.Geometry.RoundedRectangle,
        PptxCore.Geometry.Ellipse => ShapeCrawler.Geometry.Ellipse,
        PptxCore.Geometry.Triangle => ShapeCrawler.Geometry.Triangle,
        PptxCore.Geometry.Diamond => ShapeCrawler.Geometry.Diamond,
        PptxCore.Geometry.Arrow => ShapeCrawler.Geometry.RightArrow,
        PptxCore.Geometry.Line => ShapeCrawler.Geometry.Line,
        _ => ShapeCrawler.Geometry.Rectangle
    };
}

public enum Geometry
{
    Rectangle,
    RoundedRectangle,
    Ellipse,
    Triangle,
    Diamond,
    Arrow,
    Line
}
