# Angri450.Nong.Docx

农学生文档生成库——OpenXML 底层直接控制，不是 COM 套壳。纯 .NET，零外部依赖。

## 为什么做这个

写论文最痛苦的不是内容，是格式。

市面上的方案：python-docx 太底层、pandoc 不可逆、COM 必须装 Office。我们要的是：**内容写完，格式一步到位**。

## 核心能力

### StyleBuilder.BuildFromJson — 一键排版
```csharp
StyleBuilder.BuildFromJson(sp.Styles, "formats/life-sciences-contest.json");
// 换格式 = 换 JSON。竞赛论文→期刊论文→学位论文，三秒切。
```

### DocumentWriter — 链式写内容
```csharp
new DocumentWriter(body)
    .Title("氮肥运筹对水稻产量及氮素利用效率的影响")
    .EnglishTitle("Effects of nitrogen management on rice yield and nitrogen use efficiency")
    .Abstract("以粳稻品种为材料，设置4个氮肥处理...")
    .Keywords("水稻；氮肥运筹；产量；氮素利用效率")
    .Heading("引言", 1).Body("正文[1]。")
    .Table("不同氮肥处理下水稻产量构成因素", 1, headers, rows)
    .Figure("图1 实验设计示意图", 1)
    .Heading("讨论", 1)
    .BibHeading().References("作者. 标题[J]. 期刊, 年.");
// 链式调用，[N] 自动上标，Heading 自动编号
```

### ImageEmbedder — 图片嵌入
```csharp
ImageEmbedder.EmbedImages(body, mainPart, new[] { "fig1.png", "fig2.png" });
// 前两张图自动 1x2 无边框表格并排，第3张起纵向排列
// 自动识别 png/jpeg/gif/bmp/tiff，宽度约束防溢出
```

### TemplateEngine — 文档解剖
```csharp
var result = TemplateEngine.Analyze("paper.docx");
// 返回：段落结构 + 格式指纹 + 格式污染检测

var files = TemplateEngine.ExtractImages("paper.docx", "images/");
// 提取文档中所有图片
```

### WordPreview — 生成即诊断
```csharp
var r = WordPreview.Preview("paper.docx");
Console.WriteLine(r.Text);       // 结构化文本预览
// r.Warnings 检查：元素顺序、图片链接、CJK 字体、批注残留...
```

### ElementOrder.RectifyTree — 底层修复
OpenXML 对元素顺序有严格要求。Word 宽容打开但 Google Docs/WPS 可能乱。Save 前自动修正。

## 快速开始

```powershell
dotnet add package Angri450.Nong.Docx
```

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocxCore;

using var doc = WordprocessingDocument.Create("paper.docx", WordprocessingDocumentType.Document);
var body = new Body();
doc.AddMainDocumentPart().Document = new Document(body);

var sp = doc.MainDocumentPart!.AddNewPart<StyleDefinitionsPart>();
sp.Styles = new Styles();
StyleBuilder.BuildFromJson(sp.Styles, "formats/life-sciences-contest.json");

var w = new DocumentWriter(body);
w.Title("论文标题").Abstract("摘要...").Keywords("A；B；C")
 .Heading("引言", 1).Body("正文[1]。")
 .BibHeading().References("作者. 标题[J]. 期刊, 年.");

ImageEmbedder.EmbedImages(body, doc.MainDocumentPart, new[] { "fig1.png", "fig2.png" });

body.Append(new SectionProperties(/* A4 2cm margin */));
ElementOrder.RectifyTree(body);
doc.MainDocumentPart.Document.Save();
```

## 四件套

| 包 | 用途 |
|----|------|
| `Angri450.Nong.Docx` | Word 文档（本包） |
| `Angri450.Nong.Excel` | Excel 表格 |
| `Angri450.Nong.Pptx` | PowerPoint 演示 |
| `Angri450.Nong.Chart` | 统计分析 + 图表生成 |

统一 API 风格，统一 Preview→Build→Validate 流水线。Skill 做编排，.NET CLI 做确定性工作。

## 支持的格式模板

- 生命科学竞赛（限 4 页，无个人信息）
- 中文期刊（GB/T 7714 顺序编码制）
- 学位论文（小四宋体，1.5 倍行距）
- 本科课程论文

换格式 = 换 format.json，不改代码。

## 协议

Apache-2.0。基于开源的 [DocumentFormat.OpenXml SDK](https://github.com/dotnet/Open-XML-SDK)。
