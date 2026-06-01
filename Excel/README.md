# Angri450.Nong.Excel

Chainable Excel generation API over ClosedXML.

## Install

```bash
dotnet add package Angri450.Nong.Excel
```

## Quick Start

```csharp
using ClosedXML.Excel;
using ExcelCore;

var wb = new XLWorkbook();
ExcelBuilder.Sheet(wb, "Data")
    .Headers("Name", "Score", "Grade")
    .Data(new[] { new[] { "Alice", "95", "A" }, new[] { "Bob", "87", "B+" } })
    .ColumnWidths(15, 10, 10)
    .HeaderStyle("#1F4E79", "#FFFFFF")
    .AlternatingRows(2, "#F5F5F5");
wb.SaveAs("output.xlsx");
```

## Features

- **SheetBuilder**: chainable API — Headers, Data, ColumnWidths, HeaderStyle, AlternatingRows, FreezePanes, MergeCells
- **AdvancedBuilder**: pivot tables, sparklines, auto-filters, comments, hyperlinks, rich text, sheet protection, named ranges, sorting, print setup
- **StylePresets**: Mono, Finance, Academic themes
- **FormulaValidator**: formula validation with pre-save evaluation
