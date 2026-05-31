using System;
using System.Collections.Generic;
using System.Linq;

namespace DiagramCore.Layout;

/// <summary>
/// 力导向图布局算法 (Force-Directed Layout)
/// 基于 Fruchterman-Reingold 算法
/// </summary>
public class ForceDirectedLayout
{
    private const int MaxIterations = 300;
    private const double InitialTemperature = 100;
    private const double CoolingFactor = 0.95;
    private const double AttractiveForceConstant = 0.1;
    private const double RepulsiveForceConstant = 1000;

    public void Layout(Models.Graph graph, double width = 800, double height = 600)
    {
        if (graph.Nodes.Count == 0) return;

        var nodes = graph.Nodes;
        var edges = graph.Edges;

        // 随机初始化位置
        var random = new Random(42);
        foreach (var node in nodes)
        {
            node.X = random.NextDouble() * width;
            node.Y = random.NextDouble() * height;
        }

        double temperature = InitialTemperature;

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            // 计算斥力（所有节点对之间）
            var displacements = new Dictionary<string, (double dx, double dy)>();
            foreach (var node in nodes)
                displacements[node.Id] = (0, 0);

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var dx = nodes[i].X - nodes[j].X;
                    var dy = nodes[i].Y - nodes[j].Y;
                    var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);

                    var force = RepulsiveForceConstant / (dist * dist);
                    var fx = (dx / dist) * force;
                    var fy = (dy / dist) * force;

                    var disp1 = displacements[nodes[i].Id];
                    displacements[nodes[i].Id] = (disp1.dx + fx, disp1.dy + fy);

                    var disp2 = displacements[nodes[j].Id];
                    displacements[nodes[j].Id] = (disp2.dx - fx, disp2.dy - fy);
                }
            }

            // 计算引力（沿边）
            foreach (var edge in edges)
            {
                var from = graph.GetNode(edge.From);
                var to = graph.GetNode(edge.To);
                if (from == null || to == null) continue;

                var dx = to.X - from.X;
                var dy = to.Y - from.Y;
                var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);

                var force = dist * AttractiveForceConstant;
                var fx = (dx / dist) * force;
                var fy = (dy / dist) * force;

                var dispFrom = displacements[from.Id];
                displacements[from.Id] = (dispFrom.dx + fx, dispFrom.dy + fy);

                var dispTo = displacements[to.Id];
                displacements[to.Id] = (dispTo.dx - fx, dispTo.dy - fy);
            }

            // 应用位移（限制在温度范围内）
            foreach (var node in nodes)
            {
                var (dx, dy) = displacements[node.Id];
                var dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist > 0)
                {
                    var scale = Math.Min(dist, temperature) / dist;
                    node.X += dx * scale;
                    node.Y += dy * scale;
                }

                // 限制在画布内
                node.X = Math.Max(50, Math.Min(width - 50, node.X));
                node.Y = Math.Max(50, Math.Min(height - 50, node.Y));
            }

            temperature *= CoolingFactor;
        }
    }
}
