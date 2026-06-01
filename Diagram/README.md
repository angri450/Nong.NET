# Angri450.Nong.Diagram

Scientific diagram generator — flowcharts, network graphs, phylogenetic trees.

## Install

```bash
dotnet add package Angri450.Nong.Diagram
```

## Quick Start

```csharp
using DiagramCore;
using DiagramCore.Models;

// Flowchart
var graph = new Graph();
graph.AddNode("A", "Sample Collection");
graph.AddNode("B", "DNA Extraction");
graph.AddNode("C", "PCR Amplification");
graph.AddEdge("A", "B");
graph.AddEdge("B", "C");
DiagramBuilder.Flowchart(graph, "flowchart.png", 800, 600);

// Phylogenetic tree (Newick format)
var newick = "((Human:0.1,Chimp:0.12):0.05,(Gorilla:0.15,Orangutan:0.2):0.1);";
DiagramBuilder.PhylogeneticTree(newick, "tree.png", radial: false, 800, 600);
```
