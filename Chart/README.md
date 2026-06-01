# Angri450.Nong.Chart

Statistical chart generation for academic papers. 18 chart types with ANOVA + Duncan MRT.

## Install

```bash
dotnet add package Angri450.Nong.Chart
```

## Quick Start

```csharp
using ChartCore;

var data = new Dictionary<string, List<double>> {
    ["Treatment A"] = new() { 95, 88, 92, 78, 85 },
    ["Treatment B"] = new() { 72, 68, 75, 70, 73 },
    ["Treatment C"] = new() { 55, 58, 52, 60, 57 },
};

ChartBuilder.BarChart(data, "Treatment Effects", "Height (cm)", "chart.png");

// ANOVA + Duncan MRT
var analysis = StatsEngine.FullAnalysis(data);
analysis.Print();
ChartBuilder.BarChartWithSignificance(data, analysis.Duncan.Labels, "Effects", "cm", "chart-sig.png");
```

## Chart Types

Bar, pie, donut, line, area, scatter, box plot, histogram, radar, candlestick, bubble, heatmap, gauge, coxcomb, lollipop, population, function plot, error bar.
