using System.IO.Compression;
using System.Xml.Linq;

namespace DocxCore;

/// <summary>
/// Image wrap control: converts inline drawings to floating anchors with
/// configurable wrap modes. Pure OOXML — no COM, no SkiaSharp.
///
/// Wrap modes: square (四周型), topAndBottom (上下型), tight (紧密型),
/// through (穿越型), behind (衬于文字下方), inFront (浮于文字上方).
/// </summary>
public static class DocxImageWrap
{
    public record WrapOptions
    {
        public string? Mode { get; init; }     // "square" (default), "topAndBottom", "tight", "through", "behind", "inFront", "inline"
        public double? OffsetMm { get; init; } // distance from text in mm (default 3)
        public string? AlignH { get; init; }   // "left", "center", "right" (default center)
        public string? AlignV { get; init; }   // "top", "center", "bottom" (default top)
    }

    public record WrapResult(int ImagesConverted, int ImagesTotal, List<string> Changes);

    public static WrapResult Apply(string inputPath, string outputPath, WrapOptions opts)
    {
        if (opts.Mode == "inline" || opts.Mode == null)
            return new WrapResult(0, 0, new() { "no wrap mode specified" });

        File.Copy(inputPath, outputPath, true);
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var docEntry = zip.GetEntry("word/document.xml")!;
        XDocument doc;
        using (var s = docEntry.Open()) { doc = XDocument.Load(s); }

        var allInlines = doc.Descendants()
            .Where(e => e.Name.LocalName == "inline").ToList();
        int total = allInlines.Count;
        int converted = 0;
        var changes = new List<string>();

        long offsetEmu = (long)((opts.OffsetMm ?? 3) * 36000); // mm → EMU

        foreach (var il in allInlines)
        {
            var drawing = il.Parent; // wp:inline → w:drawing
            if (drawing == null) continue;

            // Get original extent
            var extent = il.Elements().FirstOrDefault(e => e.Name.LocalName == "extent");
            var cx = extent?.Attribute("cx")?.Value ?? "0";
            var cy = extent?.Attribute("cy")?.Value ?? "0";
            var docPrId = il.Elements().FirstOrDefault(e => e.Name.LocalName == "docPr")?.Attribute("id")?.Value ?? "0";

            // Build anchor element replacing inline
            XNamespace wp = il.Name.Namespace;

            var anchor = new XElement(wp + "anchor",
                new XAttribute("distT", offsetEmu),
                new XAttribute("distB", offsetEmu),
                new XAttribute("distL", offsetEmu),
                new XAttribute("distR", offsetEmu),
                new XAttribute("simplePos", "0"),
                new XAttribute("relativeHeight", "0"),
                new XAttribute("behindDoc", opts.Mode == "behind" ? "1" : "0"),
                new XAttribute("locked", "0"),
                new XAttribute("layoutInCell", "1"),
                new XAttribute("allowOverlap", "1"),
                // Position: center horizontally
                new XElement(wp + "simplePos", new XAttribute("x", "0"), new XAttribute("y", "0")),
                new XElement(wp + "positionH",
                    new XAttribute("relativeFrom", "column"),
                    new XElement(wp + "posOffset", "0")),
                new XElement(wp + "positionV",
                    new XAttribute("relativeFrom", "paragraph"),
                    new XElement(wp + "posOffset", "0"))
            );

            // Copy size
            anchor.Add(new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)));
            anchor.Add(new XElement(wp + "effectExtent",
                new XAttribute("l", "0"), new XAttribute("t", "0"),
                new XAttribute("r", "0"), new XAttribute("b", "0")));

            // Wrap type
            if (opts.Mode == "square")
                anchor.Add(new XElement(wp + "wrapSquare", new XAttribute("wrapText", "bothSides")));
            else if (opts.Mode == "topAndBottom")
                anchor.Add(new XElement(wp + "wrapTopAndBottom"));
            else if (opts.Mode == "tight")
                anchor.Add(new XElement(wp + "wrapTight", new XAttribute("wrapText", "bothSides")));
            else if (opts.Mode == "through")
                anchor.Add(new XElement(wp + "wrapThrough", new XAttribute("wrapText", "bothSides")));
            else if (opts.Mode == "behind")
                anchor.Add(new XElement(wp + "wrapNone"));
            else if (opts.Mode == "inFront")
                anchor.Add(new XElement(wp + "wrapNone"));
            else
                anchor.Add(new XElement(wp + "wrapSquare", new XAttribute("wrapText", "bothSides")));

            // Copy docPr
            anchor.Add(new XElement(wp + "docPr",
                new XAttribute("id", docPrId),
                new XAttribute("name", $"Picture {docPrId}")));
            anchor.Add(new XElement(wp + "cNvGraphicFramePr"));

            // Copy the graphic content
            var graphic = il.Elements().FirstOrDefault(e => e.Name.LocalName == "graphic");
            if (graphic != null)
                anchor.Add(graphic);

            // Set alignment
            var posH = anchor.Element(wp + "positionH");
            if (posH != null)
            {
                string ah = (opts.AlignH ?? "center").ToLower();
                if (ah == "left") posH.SetAttributeValue("relativeFrom", "column");
                else if (ah == "center") posH.SetAttributeValue("relativeFrom", "column");
                else posH.SetAttributeValue("relativeFrom", "column");
            }

            // Replace inline with anchor in the drawing
            il.Remove();
            drawing.Add(anchor);
            converted++;
        }

        changes.Add($"converted {converted}/{total} images to {opts.Mode} wrap");

        docEntry.Delete();
        var newEntry = zip.CreateEntry("word/document.xml");
        using (var ws = newEntry.Open()) doc.Save(ws);

        return new WrapResult(converted, total, changes);
    }
}
