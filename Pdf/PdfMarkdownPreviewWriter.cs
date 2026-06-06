using System.Text;

namespace PdfCore;

public static class PdfMarkdownPreviewWriter
{
    public static string Write(PdfDocumentModel document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- Lossy compatibility preview. Source of truth: content.nongmark + canonical JSON streams. -->");
        sb.AppendLine();

        foreach (var block in document.Blocks.OrderBy(b => b.Index))
        {
            switch (block.Kind)
            {
                case "heading":
                    sb.AppendLine("# " + (block.Text ?? ""));
                    sb.AppendLine();
                    break;
                case "image":
                    sb.AppendLine($"![{block.Text ?? "image"}]({block.AssetPath ?? ""})");
                    sb.AppendLine();
                    break;
                case "pageBreak":
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(block.Text))
                    {
                        sb.AppendLine(block.Text);
                        sb.AppendLine();
                    }
                    break;
            }
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }
}
