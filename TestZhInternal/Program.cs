using DiagramCore;
using DiagramCore.Models;

var graph = new Graph { Title = "作物抗逆性分子调控网络" };
graph.AddNode("转录因子", "转录因子\nOsNAC6");
graph.AddNode("启动子", "下游启动子\n顺式元件");
graph.AddNode("功能基因", "抗逆功能基因\nOsLEA3");
graph.AddNode("代谢物", "渗透调节物质\n脯氨酸积累");
graph.AddEdge("转录因子", "启动子");
graph.AddEdge("启动子", "功能基因");
graph.AddEdge("功能基因", "代谢物");
DiagramBuilder.NetworkGraph(graph, "test-zh-network.png", 900, 600);
Console.WriteLine("Generated.");
