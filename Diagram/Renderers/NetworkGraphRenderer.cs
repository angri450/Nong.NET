using SkiaSharp;
using DiagramCore.Models;
using DiagramCore.Layout;
using SkiaSharp.HarfBuzz;

namespace DiagramCore.Renderers;

public class NetworkGraphRenderer : IRenderer
{
    private readonly Graph _graph;
    private readonly ForceDirectedLayout _layout = new();

    public NetworkGraphRenderer(Graph graph)
    {
        _graph = graph;
    }

    public void Render(string outputPath, int width = 800, int height = 600)
    {
        using var bitmap = RenderToBitmap(width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }

    public SKBitmap RenderToBitmap(int width = 800, int height = 600)
    {
        _layout.Layout(_graph, width, height);

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // 绘制边
        using var edgePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColor.Parse("#CCCCCC"),
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        foreach (var edge in _graph.Edges)
        {
            var from = _graph.GetNode(edge.From);
            var to = _graph.GetNode(edge.To);
            if (from == null || to == null) continue;

            canvas.DrawLine((float)from.X, (float)from.Y, (float)to.X, (float)to.Y, edgePaint);
        }

        // 绘制节点
        using var nodePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 12,
            TextAlign = SKTextAlign.Center
        };

        foreach (var node in _graph.Nodes)
        {
            nodePaint.Color = SKColor.Parse(node.FillColor);
            canvas.DrawCircle((float)node.X, (float)node.Y, 20, nodePaint);

            nodePaint.Style = SKPaintStyle.Stroke;
            nodePaint.Color = SKColor.Parse(node.StrokeColor);
            nodePaint.StrokeWidth = 2;
            canvas.DrawCircle((float)node.X, (float)node.Y, 20, nodePaint);
            nodePaint.Style = SKPaintStyle.Fill;

            textPaint.Color = SKColor.Parse(node.TextColor);
            canvas.DrawShapedText(node.Label, (float)node.X, (float)node.Y + 4, textPaint);
        }

        // 标题
        if (!string.IsNullOrEmpty(_graph.Title))
        {
            textPaint.TextSize = 20;
            textPaint.Color = SKColors.Black;
            canvas.DrawShapedText(_graph.Title, width / 2, 30, textPaint);
        }

        return bitmap;
    }
}
