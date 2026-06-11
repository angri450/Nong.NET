using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Merges nearby standalone image paragraphs and scales their inline drawings
/// to fit side-by-side within page text width. Pure OOXML — no COM, no SkiaSharp.
///
/// Operates in two passes:
/// 1. Find consecutive image paragraphs (at most 1 caption apart), merge drawing runs
/// 2. Scale all multi-drawing paragraphs to fit page width with a configurable gap
/// </summary>
public static class DocxImageFitter
{
    public static DocxFitResult FitImages(string inputPath, string outputPath, double gapMm = 2.0)
    {
        File.Copy(inputPath, outputPath, true);

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;

        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        // 1. Calculate page text width in EMU
        long textWidthEmu = GetTextWidthEmu(doc);

        // 2. Get all body paragraphs
        var bodyParas = doc.Descendants()
            .Where(e => e.Name.LocalName == "p" && e.Parent?.Name.LocalName == "body")
            .ToList();

        long gapEmu = (long)(gapMm * 36000);

        var modified = new List<FitParagraphResult>();
        int i = 0;

        while (i < bodyParas.Count)
        {
            // Is this an image-only paragraph?
            var inlines = bodyParas[i].Descendants()
                .Where(e => e.Name.LocalName == "inline").ToList();

            if (inlines.Count == 0) { i++; continue; }

            // Look ahead for another image paragraph within 3 paragraphs
            List<XElement> drawingsToMerge = new();
            int j = i + 1;

            while (j < bodyParas.Count && j <= i + 3)
            {
                var nextInlines = bodyParas[j].Descendants()
                    .Where(e => e.Name.LocalName == "inline").ToList();

                if (nextInlines.Count >= 1 && drawingsToMerge.Count < 4)
                {
                    // Found another image paragraph — collect its drawing runs
                    foreach (var il in nextInlines)
                        drawingsToMerge.Add(il.Parent?.Parent!); // inline → drawing → r

                    // Mark this paragraph for deletion
                    bodyParas[j].Remove();
                    j++;
                    if (drawingsToMerge.Count >= 4) break; // at most 4 images
                }
                else if (nextInlines.Count == 0 && !HasText(bodyParas[j]) && drawingsToMerge.Count == 0)
                {
                    // Empty paragraph between images — skip it (keep scanning)
                    j++;
                }
                else
                {
                    // Text paragraph found — stop scanning only if we already found images
                    if (drawingsToMerge.Count > 0) break;
                    j++;
                }
            }

            // Move drawing runs from collected paragraphs into the current paragraph
            if (drawingsToMerge.Count > 0)
            {
                // Move the runs into the current paragraph (before the paragraph's end)
                foreach (var r in drawingsToMerge)
                    bodyParas[i].Add(r);

                // Re-collect inlines
                inlines = bodyParas[i].Descendants()
                    .Where(e => e.Name.LocalName == "inline").ToList();
            }

            // Scale if needed
            if (inlines.Count >= 2)
            {
                var dims = new List<(long, long)>();
                foreach (var il in inlines)
                {
                    var ext = il.Elements().FirstOrDefault(e => e.Name.LocalName == "extent");
                    if (ext == null) continue;
                    if (long.TryParse(ext.Attribute("cx")?.Value, out long cx) &&
                        long.TryParse(ext.Attribute("cy")?.Value, out long cy) && cx > 0)
                        dims.Add((cx, cy));
                }

                long totalWidth = dims.Sum(d => d.Item1);
                if (totalWidth > textWidthEmu && dims.Count >= 2)
                {
                    long totalGap = gapEmu * (dims.Count - 1);
                    double scale = (double)(textWidthEmu - totalGap) / totalWidth;

                    if (scale > 0 && scale < 1.0)
                    {
                        var scaled = new List<(long, long, long, long)>();
                        foreach (var d in dims)
                        {
                            long nw = (long)(d.Item1 * scale);
                            long nh = (long)(d.Item2 * scale);
                            scaled.Add((nw, nh, d.Item1, d.Item2));
                        }

                        int mIdx = 0;
                        foreach (var il in inlines)
                        {
                            if (mIdx >= scaled.Count) break;
                            var (nw, nh, _, _) = scaled[mIdx++];

                            var ext = il.Elements().FirstOrDefault(e => e.Name.LocalName == "extent");
                            if (ext != null)
                            {
                                ext.SetAttributeValue("cx", nw);
                                ext.SetAttributeValue("cy", nh);
                            }

                            var xfrmExt = il.Descendants()
                                .FirstOrDefault(e => e.Name.LocalName == "ext"
                                    && e.Parent?.Name.LocalName == "xfrm");
                            if (xfrmExt != null)
                            {
                                xfrmExt.SetAttributeValue("cx", nw);
                                xfrmExt.SetAttributeValue("cy", nh);
                            }
                        }

                        modified.Add(new FitParagraphResult(i, dims.Count,
                            totalWidth, textWidthEmu, scale, scaled.ToArray()));
                    }
                }
            }

            i++; // advance past current paragraph (even if it was modified)
        }

        // 3. Write back
        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open())
            doc.Save(ws);

        return new DocxFitResult(modified);
    }

    static bool HasText(XElement para)
    {
        var text = para.Descendants()
            .Where(e => e.Name.LocalName == "t")
            .Select(t => t.Value)
            .ToList();
        return text.Any(t => !string.IsNullOrWhiteSpace(t));
    }

    static long GetTextWidthEmu(XDocument doc)
    {
        var sectPr = doc.Descendants()
            .Where(e => e.Name.LocalName == "sectPr")
            .LastOrDefault();
        if (sectPr == null) return 5_580_380;

        var pgSz = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgSz");
        var pgMar = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName == "pgMar");

        long pw = 11906, ml = 1701, mr = 1417;
        if (pgSz != null)
            foreach (var a in pgSz.Attributes())
                if (a.Name.LocalName == "w") long.TryParse(a.Value, out pw);
        if (pgMar != null)
            foreach (var a in pgMar.Attributes())
            {
                if (a.Name.LocalName == "left") long.TryParse(a.Value, out ml);
                if (a.Name.LocalName == "right") long.TryParse(a.Value, out mr);
            }

        long tw = pw - ml - mr;
        return tw > 0 ? tw * 635 : 5_580_380;
    }
}

public sealed record FitParagraphResult(
    int ParagraphIndex, int ImageCount,
    long OriginalTotalEmu, long PageTextEmu,
    double ScaleFactor,
    (long NewWidth, long NewHeight, long OldWidth, long OldHeight)[] Dimensions);

public sealed record DocxFitResult(List<FitParagraphResult> Modified)
{
    public int ParagraphsModified => Modified.Count;
    public int ImagesScaled => Modified.Sum(m => m.ImageCount);
}
