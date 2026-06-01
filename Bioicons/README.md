# Angri450.Nong.Bioicons

40 SVG scientific icons embedded as assembly resources.

## Install

```bash
dotnet add package Angri450.Nong.Bioicons
```

## Usage

```csharp
using Bioicons;

var categories = IconProvider.GetCategories();
// ["Arrows", "Biology", "Chemistry", "Experimental", "LabEquipment", "Medical"]

var icons = IconProvider.GetIcons("Biology");
// ["antibody", "bacteria", "cell", "dna", ...]

string svg = IconProvider.GetSvg("Biology", "cell");
IconProvider.SaveSvg("Biology", "dna", "dna.svg");
```
