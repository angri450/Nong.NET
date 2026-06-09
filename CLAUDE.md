# CLAUDE.md — Nong.Cli.Net

Pure .NET scientific document generation toolkit. Zero JavaScript. One merged foundation DLL.

## 仓库

- GitHub: `https://github.com/angri450/Nong.Cli.Net`
- Gitee: `https://gitee.com/angri450/Nong.Cli.Net`
- GitCode: `git@gitcode.com:angri450/Nong.Cli.Net.git`
- 主分支: `main`
- 协议: Apache-2.0

## 当前进度（2026-06-08）

- 发布版本: `4.0.0`
- 当前主线: GitHub / Gitee / GitCode 的 `master`
- 4.0.0 NuGet 主线包:
  - `Angri450.Nong.ThirdParty 4.0.0`
  - `Angri450.Nong.Bioicons 4.0.0`
  - `Angri450.Nong.Genre 4.0.0`
  - `Angri450.Nong.Pandoc 4.0.0`
  - `Angri450.Nong.Docx 4.0.0`
  - `Angri450.Nong.Excel 4.0.0`
  - `Angri450.Nong.Chart 4.0.0`
  - `Angri450.Nong.Diagram 4.0.0`
  - `Angri450.Nong.Pdf 4.0.0`
  - `Angri450.Nong.Pptx 4.0.0`
  - `Angri450.Nong.Literature 4.0.0`
  - `Angri450.Nong.MultiModal 4.0.0`
  - `Angri450.Nong.Inspect 4.0.0`
  - `Angri450.Nong.Cli 4.0.0`
  - `Angri450.Nong.OcrRuntime.WinX64 4.0.0`
- OCR runtime 已拆到独立 `Angri450.Nong.OcrRuntime` 仓库维护；该仓库维护 WinX64、LinuxX64、LinuxArm64、OsxX64、OsxArm64 五个平台包，Nong.NET 主仓库只消费已发布 runtime 版本。
- 4.0.0 发布验证见 `log/changelog/2026-06-08-nong-4.0.0-release.md`。
- 重要架构变化:
  - `chart` / `diagram` PNG 渲染已隔离到隐藏 worker: `nong __render-worker ...`
  - 主 CLI 进程只做参数校验和子进程调度，不再直接执行 SkiaSharp/ScottPlot native 渲染。
  - 如果 native 渲染崩溃，最多 worker 子进程退出，主 CLI 返回结构化错误，不拖垮主进程。
  - 默认测试套件不再运行 Chart/Diagram/OCR/PDF 的 native 图像渲染路径。

## 包结构（9 个常规包 + OCR runtime 部署包）

| 包 | 项目路径 | 说明 |
|----|---------|------|
| `Angri450.Nong.ThirdParty` | `ThirdParty/` | **地基** — 合入 15 个第三方库源码编译为单一 DLL |
| `Angri450.Nong.Excel` | `Excel/` | 链式 Excel 生成 API |
| `Angri450.Nong.Chart` | `Chart/` | 18 种图表 + ANOVA/Duncan MRT |
| `Angri450.Nong.Diagram` | `Diagram/` | 流程图、网络图、系统发育树 |
| `Angri450.Nong.Docx` | `Docx/` | Word 生成 + 论文诊断 |
| `Angri450.Nong.Pptx` | `Pptx/` | PPT 生成、10 套主题 |
| `Angri450.Nong.MultiModal` | `MultiModal/` | PaddleOCR 云 + 纯 .NET 本地 PP-OCRv5 |
| `Angri450.Nong.Bioicons` | `Bioicons/` | 40 个 SVG 科学图标 |
| `Angri450.Nong.Skill.Manager` | `SkillManager/` | Skill CLI 工具 |

OCR 本地推理另有独立 runtime 仓库和部署包，不供应用代码直接引用，只给 `nong ocr install-model` 下载/解包。

独立仓库：`C:\Users\Administrator\Documents\Github\Angri450.Nong.OcrRuntime`（计划远程仓库 `angri450/Angri450.Nong.OcrRuntime`）。

runtime 仓库维护全部 5 个平台包：

- `Angri450.Nong.OcrRuntime.WinX64`
- `Angri450.Nong.OcrRuntime.LinuxX64`
- `Angri450.Nong.OcrRuntime.LinuxArm64`
- `Angri450.Nong.OcrRuntime.OsxX64`
- `Angri450.Nong.OcrRuntime.OsxArm64`

Nong.NET 主仓库只消费已发布 runtime 包，不再维护 runtime 打包工程。

## 依赖链

```
ThirdParty (ClosedXML + OpenXml + ScottPlot + MSAGL + SkiaSharp + HarfBuzz + SixLabors + ...)
  ├── Excel   + System.IO.Packaging (NuGet)
  ├── Chart
  ├── Diagram + Bioicons
  └── Docx
       └── MultiModal

Pptx → ShapeCrawler (NuGet, 独立)
SkillManager → YamlDotNet (NuGet, 独立, CLI 工具)
```

## 开发约定

### 版本号
- 当前大版本: **4.x.x**
- 大版本号（4）全局统一，小版本号（x.x）各包独立
- 改一个包 → 只 bump 那个包的次版本号（如 4.0.0 → 4.0.1）→ 只推送那个包
- 大版本升级（4.x → 5.0.0）时才全部统一更新
- 不要为了小改动 bump 全部包
- OCR native runtime 版本独立于 CLI/Word/PDF 等小版本。`Cli/Common/OcrRuntimeVersion.cs` 必须锁定到独立 `Angri450.Nong.OcrRuntime` 仓库实际发布的 native bundle 版本；只有 Paddle/OpenCV native 内容或 runtime 安装合同变化时才在 runtime 仓库 bump 并重新发布 OCR runtime 大包。

### NuGet 发布流程（单包更新）
改一个包的代码后，按顺序：
1. 改 `<Version>`（如 `4.0.0` → `4.0.1`）
2. `dotnet build <那个项目>.csproj -c Release`
3. `dotnet pack <那个项目>.csproj -c Release -o nupkg/ --no-build`
4. `dotnet nuget push nupkg/<包名>.<新版>.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
5. `gh release create v<新版> nupkg/<包名>.<新版>.nupkg --title "v<新版>" --notes "<改动说明>" --latest`
6. `git add -A && git commit -m "release: <包名> v<新版> — <改动摘要>" && git push`

### OCR runtime 发布流程
本地 OCR 禁止 Python/pip/外部 OCR 执行文件。heavy Paddle/OpenCV native runtime 通过独立 `Angri450.Nong.OcrRuntime` 仓库维护的第一方 `Angri450.Nong.OcrRuntime.*` 包分平台部署。

Nong.NET 主仓库发布边界：

- 常规 CLI/Word/PDF/Excel/PPT 小版本发布不重新打包或推送 OCR runtime。
- `nong ocr install-model` 会继续按 `OcrRuntimeVersion.Current` 安装已发布的第一方 native bundle。
- 如果确实修改了 native bundle 内容，必须在 `Angri450.Nong.OcrRuntime` 仓库更新 `VERSION` 和 `OcrRuntime.csproj` `<Version>`、打包并发布 runtime 包，再同步更新本仓库 `Cli/Common/OcrRuntimeVersion.cs`。
- Windows/Linux/macOS 五个平台包都在 runtime 仓库维护；某个平台是否发布取决于 runtime 仓库的目标机器 smoke test 和发布策略。

验证步骤：

1. 在 `Angri450.Nong.OcrRuntime` 仓库打包并验证需要发布的平台包。
2. 推送已验证的 `Angri450.Nong.OcrRuntime.*` 包到 NuGet.org。
3. 等华为 NuGet 镜像同步。
4. 在本仓库用华为源验证：`nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json`。

默认安装路径只接受 Nong 第一方 runtime 包。上游 Sdcb/OpenCvSharp fallback 必须由用户或维护者显式加 `--allow-upstream-fallback`，不要让 agent 静默回退到旧大包链路。

### NuGet 发布流程（大版本升级，极少用）
全部 9 个包统一改 `<Version>` → 批量 build + pack + push → 一个 GitHub Release 包含全部 nupkg。

### 编译
- TargetFramework: `net10.0`（向前兼容 .NET 11+）
- 无 `global.json` SDK 锁定
- `ThirdParty.csproj` 通过 glob `**/*.cs` 包含第三方源码，排除 `bin/`、`obj/`、`common/` polyfill

### NuGet 包规范
- 每个包必须有: `<PackageReadmeFile>README.md</PackageReadmeFile>` + `<RepositoryUrl>https://github.com/angri450/Nong.NET</RepositoryUrl>`
- 每个项目目录必须有 `README.md`
- `*.csproj` 中必须有 `<None Include="README.md" Pack="true" PackagePath="/" />`

### ThirdParty 源码合入注意事项
- SkiaSharp 源码版本 (m145) 高于可用本地库 (m119)，已手动适配：`VersionConstants` 修正为 119/0，`SKTypeface` 用 `match_family_style` 替代 `legacy_create_typeface`，`SKPathBuilder` 重写为旧路径 API，`FlowchartRenderer` 用 `DrawLine` 替代 `SKPath`
- 嵌入式资源名（字体、.resx）需 `LogicalName` + `CustomToolNamespace` 对齐原始程序集命名
- `OpenXmlSourceGenerator` 保留为独立分析器项目（`netstandard2.0`），不在 ThirdParty 中合入

### 分支策略
- 只推 `master`，`main` 由人工手动合并
- 不要自动切到 `main`，也不要把 `main` 合并回 `master`（只允许把 `master` 合并到 `main`）

### 三仓库同步 (GitHub + Gitee + GitCode)
- GitHub: `https://github.com/angri450/Nong.NET`（主仓库）
- Gitee: `https://gitee.com/angri450/Nong.NET`（镜像）
- GitCode: `git@gitcode.com:angri450/Nong.NET.git`（镜像）
- 每次 push 完 GitHub master，同步执行 `git push gitee master` 和 `git push gitcode master`
- Gitee remote 名称: `gitee`
- GitCode remote 名称: `gitcode`
- 初始化（仅首次）: `git remote add gitee https://gitee.com/angri450/Nong.NET.git`

## 禁止事项
- 不要引入 JavaScript 依赖
- 不要用 Python 实现核心功能；本地 OCR 必须走纯 .NET PP-OCRv5，不恢复 `MultiModal/scripts/ocr_local.py`
- 不要为第三方库创建独立的 `.csproj` — 已全部删掉，统一走 ThirdParty
- 不要用 PowerShell 的 `-replace` 批量编辑 `.csproj` — 会损坏 XML，用 Edit 工具逐文件改

## 对项目维护者的说明

你不用自己写代码。告诉 Claude 要做什么，Claude 负责：
- 写代码、修 bug、加功能
- 改 csproj、build、pack、推送 NuGet
- 创建 GitHub Release
- 整理项目结构

你只需要：
- 决定方向（"加个饼图"、"发版"、"修那个 bug"）
- 提供 API Key（NuGet 推送时）
- 在 GitHub 网页上手动把 master 合并到 main（你想合的时候）

NuGet API Key 已保存在仓库的 CLAUDE.md 之外（环境变量 `$env:NUGET_API_KEY`），Claude 推送时会自动使用。

## 完整目录地图（新人/新会话速览）

### 你的代码（9 个项目，需要维护）

| 目录 | 关键文件 | 依赖 |
|------|---------|------|
| `ThirdParty/` | `ThirdParty.csproj` + `GlobalUsings.cs` + `DelegateProxies*.cs` + `HashCodeExt.cs` | 7 个本地库 NuGet + 2 个分析器 |
| `Excel/` | `ExcelBuilder.cs` + `AdvancedBuilder.cs` + `StylePresets.cs` + `FormulaValidator.cs` | ThirdParty + System.IO.Packaging |
| `Chart/` | `ChartBuilder.cs` + `ChartTypes.cs` + `StatsEngine.cs` + `DataLoader.cs` | ThirdParty |
| `Diagram/` | `DiagramBuilder.cs` + `Models/` + `Renderers/` + `Layout/` | ThirdParty + Bioicons |
| `Docx/` | `Builders.cs` + `DocumentWriter.cs` + `StyleBuilder.cs` + `TemplateEngine.cs` + `PaperDiagnostics.cs` | ThirdParty |
| `Pptx/` | `PresentationBuilder.cs` + `SlideBuilder.cs` + `ThemePreset.cs` + `LayoutSystem.cs` | ShapeCrawler (NuGet) |
| `MultiModal/` | `PaddleOcrVlClient.cs` + `PpOcrV5/` + `LayoutToWordConverter.cs` | Docx |
| `Bioicons/` | `IconProvider.cs` + `*.svg` (40个) | 无 |
| `SkillManager/` | `Program.cs` + `Models/` + `Tools/` + `assets/` | YamlDotNet (NuGet) |

### 第三方源码（15 个目录，不要动）

`ClosedXML/` `ClosedXML.IO/` `ClosedXML.Parser/` `ExcelNumberFormat/` `RBush/`
`DocumentFormat.OpenXml/` `DocumentFormat.OpenXml.Framework/`
`ScottPlot/` `SkiaSharp/` `HarfBuzzSharp/` `SkiaSharp.HarfBuzz/`
`MSAGL/` `MSAGL.Drawing/` `SixLabors.Fonts/` `Binding.Shared/`

这些目录只读。编译时 ThirdParty.csproj 通过 glob 自动引入。里面有 `bin/` 和 `obj/` 残留是正常的（.gitignore 已排除）。

如果第三方源码有 bug 需要修：直接改 `.cs` 文件，但**不要**为它们创建 `.csproj`。

### 基础设施（不用管）

| 目录 | 用途 |
|------|------|
| `data/` | OpenXml 源生成器的数据文件（JSON/Schema） |
| `common/` | 旧 TFM 的 polyfill（net10.0 不需要） |
| `nupkg/` | 打包输出临时目录 |
| `tests-output/` | 测试项目 + 生成文件 |
| `DocumentFormat.OpenXml.Generator/` | Roslyn 源生成器（分析器项目） |
| `DocumentFormat.OpenXml.Generator.Models/` | 源生成器模型 |
| `UnicodeTrieGenerator/` | SixLabors 用到的 Unicode 状态机 |
| `changelog/` | 版本变更记录（NuGet 包 Agent ↔ Skill Agent 通讯） |
| `.gitignore` | 排除 bin/obj/nupkg |
| `CLAUDE.md` | 就是这个文件 |
| `README.md` + `README.zh-CN.md` | 中英文仓库首页 |
| `LICENSE` | MIT |

### changelog 目录约定

`changelog/` 是 NuGet 包开发 Agent 与 Skill 开发 Agent 之间的**通讯桥梁**。

**格式**：`changelog/YYYY-MM-DD-topic.md`

**规则**：
- 每次版本发布后，写入一个或多个 changelog 文件
- 每个文件聚焦一个主题（TFM 变更、新功能、Bug 修复）
- 文件名格式：`日期-主题.md`（日期为创建日期，主题用英文小写 + 连字符）
- 文件内容包含：时间、影响包、变更类型、详细说明、技能须知

**Skill Agent 职责**：
- 每次接管时，先读取 `changelog/` 目录中比上次更新时间晚的文件
- 根据"技能须知"更新对应 skill
- 更新后记录已处理的文件

**NuGet Agent 职责**：
- 每次发版后，创建对应的 changelog 文件
- 写清楚"影响哪个包"、"技能需要怎么改"

1. **SkiaSharp 版本不匹配**: 源码是 m145，NuGet 本地库最高只有 m119。`SKTypeface.cs`、`SKPathBuilder.cs`、`SkiaApi.cs`、`VersionConstants.cs`、`FlowchartRenderer.cs` 已手动降级适配。
2. **字体资源命名**: ClosedXML 的 `DefaultGraphicEngine.cs` 硬编码了资源名 `ClosedXML.Graphics.Fonts.xxx.ttf`，ThirdParty.csproj 必须用 `LogicalName` 覆盖默认的 `ThirdParty.xxx.ttf`。
3. **验证资源命名**: `ValidationResources.resx` 和 `ExceptionMessages.resx` 需要 `CustomToolNamespace` 对齐 `DocumentFormat.OpenXml` 命名空间。
4. **HarfBuzz + SkiaSharp 共享 partial class**: `DelegateProxies.shared.cs` 原本用 `#if HARFBUZZ` 条件编译，合并后拆成两份：`DelegateProxiesSkia.cs` + `DelegateProxiesHarfBuzz.cs`。
5. **ScottPlot 全局 usings**: `ScottPlot/Usings.cs` 包含 `global using System.IO;`，与 MSAGL 的 `Path` 类型冲突，已排除该文件，手动在 `GlobalUsings.cs` 中补回需要的 using。
6. **MSAGL 命名冲突**: `Path` 与 `System.IO.Path` 冲突，在 `RectilinearEdgeRouter.cs` 和 `StaticGraphUtility.cs` 中加了 using alias。`Timer` 与 `System.Threading.Timer` 冲突，在 `GlobalUsings.cs` 中加了全局 alias。
7. **OpenXml 源生成器**: 必须保留为独立项目（Roslyn 分析器），不能在 ThirdParty 中合入。`ThirdParty.csproj` 通过 `OutputItemType="Analyzer"` 引用。
8. **PowerShell 批量编辑 csproj 会损坏 XML**: 已写入禁止事项。用 Edit 工具逐个文件改。
9. **Chart/Diagram native 渲染隔离**: `SkiaSharp/ScottPlot` 仍是 native 图像链。CLI 命令层必须通过 `NativeRenderWorkerHost` 启动隐藏 worker，不要把 `new Plot()`、`SavePng()`、`DiagramBuilder.*` 重新放回普通命令处理器。
10. **默认测试不跑 native 图像渲染**: 不要把 Chart/Diagram/OCR/PDF render 的真实 PNG 生成测试加回默认 `dotnet test`。需要人工验证时用单独 smoke 或 worker 子进程。
