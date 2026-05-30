using ClosedXML.Excel;

namespace ExcelCore;

public static class ExcelBuilder
{
    public static SheetBuilder Sheet(XLWorkbook wb, string name) => new(wb, name);
}

public class SheetBuilder
{
    private readonly IXLWorksheet _sheet;

    internal SheetBuilder(XLWorkbook wb, string name) { _sheet = wb.AddWorksheet(name); }

    public SheetBuilder Headers(params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++) _sheet.Cell(1, i + 1).Value = headers[i];
        return this;
    }

    public SheetBuilder Data(string[][] data)
    {
        for (var r = 0; r < data.Length; r++)
            for (var c = 0; c < data[r].Length; c++)
                _sheet.Cell(r + 2, c + 1).Value = data[r][c];
        return this;
    }

    public SheetBuilder Table() { _sheet.RangeUsed()?.CreateTable(); return this; }

    public SheetBuilder ApplyPreset(string preset)
    {
        var colors = StylePresets.Get(preset);
        var headerRow = _sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml(colors.headerBg);
        headerRow.Style.Font.FontColor = XLColor.FromHtml(colors.headerText);
        return this;
    }

    public SheetBuilder AutoFit() { _sheet.Columns().AdjustToContents(); return this; }

    public SheetBuilder FreezePanes(int rows = 1, int cols = 0) { _sheet.SheetView.Freeze(rows, cols); return this; }

    public SheetBuilder Widths(params double[] widths)
    {
        for (var i = 0; i < widths.Length; i++) _sheet.Column(i + 1).Width = widths[i];
        return this;
    }

    public SheetBuilder Formula(string cell, string formula) { _sheet.Cell(cell).FormulaA1 = formula; return this; }

    public IXLWorksheet Build() => _sheet;

    public SheetBuilder Chart(string type, Func<(string title, string[] labels, double[] values)> dataFunc)
    {
        var d = dataFunc();
        var chart = _sheet.Chart;
        _ = _sheet.LastRowUsed()?.RowNumber() ?? 2;
        // Chart generation placeholder
        return this;
    }
}
