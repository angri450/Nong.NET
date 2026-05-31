using SkiaSharp;
using DiagramCore.Models;
using DiagramCore.Layout;
using SkiaSharp.HarfBuzz;

namespace DiagramCore.Renderers;

public class TreeRenderer : IRenderer
{
    private readonly NewickTree _root;
    private readonly TreeLayout _layout = new();
    private readonly bool _radial;

    public TreeRenderer(NewickTree root, bool radial = false)
    {
        _root = root;
        _radial = radial;
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
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        if (_radial)
        {
            double radius = Math.Min(width, height) * 0.35;
            _layout.LayoutRadial(_root, width / 2, height / 2, radius);
        }
        else
        {
            _layout.LayoutRectangular(_root, 50, 50);
        }

        // 绘制分支
        using var branchPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColor.Parse("#333333"),
            StrokeWidth = 2,
            IsAntialias = true
        };

        DrawBranches(canvas, _root, branchPaint);

        // 绘制叶节点标签
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 12,
            Color = SKColors.Black
        };

        DrawLabels(canvas, _root, textPaint);

        return bitmap;
    }

    private void DrawBranches(SKCanvas canvas, NewickTree node, SKPaint paint)
    {
        foreach (var child in node.Children)
        {
            if (_radial)
            {
                canvas.DrawLine((float)node.X, (float)node.Y, (float)child.X, (float)child.Y, paint);
            }
            else
            {
                // 矩形树：先水平再垂直
                canvas.DrawLine((float)node.X, (float)child.Y, (float)child.X, (float)child.Y, paint);
                canvas.DrawLine((float)node.X, (float)node.Y, (float)node.X, (float)child.Y, paint);
            }

            DrawBranches(canvas, child, paint);
        }
    }

    private void DrawLabels(SKCanvas canvas, NewickTree node, SKPaint paint)
    {
        if (node.IsLeaf)
        {
            canvas.DrawShapedText(node.Name, (float)node.X + 5, (float)node.Y + 4, paint);
        }
        else
        {
            foreach (var child in node.Children)
                DrawLabels(canvas, child, paint);
        }
    }
}
