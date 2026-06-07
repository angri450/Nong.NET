# CLAUDE.md — Nong.NET

Pure .NET scientific document generation toolkit. Zero JavaScript. One merged foundation DLL.

## 仓库

- GitHub: `https://github.com/angri450/Nong.NET`
- Gitee: `https://gitee.com/angri450/Nong.NET`
- GitCode: `git@gitcode.com:angri450/Nong.NET.git`
- 主分支: `master`
- 协议: Apache-2.0

## 当前进度（2026-06-07）

- 最新发布提交: `73730c6e7c976fb333a54a35a37ee25edd0118c5`
- 发布版本: `3.2.5`
- 代码已推送: GitHub / Gitee / GitCode 的 `master` 均指向 `73730c6`
- NuGet 已发布主线包:
  - `Angri450.Nong.ThirdParty 3.2.5`
  - `Angri450.Nong.Pdf 3.2.5`
  - `Angri450.Nong.MultiModal 3.2.5`
  - `Angri450.Nong.Literature 3.2.5`
  - `Angri450.Nong.OcrRuntime.WinX64 3.2.5`
  - `Angri450.Nong.Cli 3.2.5`
- NuGet 误发布但后续不再作为主线目标的包:
  - `Angri450.Nong.OcrRuntime.LinuxX64 3.2.5`
  - `Angri450.Nong.OcrRuntime.LinuxArm64 3.2.5`
  - `Angri450.Nong.OcrRuntime.OsxX64 3.2.5`
  - `Angri450.Nong.OcrRuntime.OsxArm64 3.2.5`
  - 当前 `NUGET_API_KEY` 只有 push 权限，unlist/delete 返回 403；后续需用有 unlist 权限的 key 或网页手动隐藏。
- 验证通过:
  - `Cli.Tests`: 83 passed
  - `Tests`: 110 passed
  - 本地 tool smoke: `nong --version`、`nong commands --json`、`nong lit parse`、`nong ocr install-model --dry-run`
  - worker smoke: `nong chart bar ...`、`nong diagram tree ...` 生成 PNG
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

OCR 本地推理另有部署包，不供应用代码直接引用，只给 `nong ocr install-model` 下载/解包。

当前主线策略：**只发布 Windows x64 runtime 包**。

- `Angri450.Nong.OcrRuntime.WinX64`

非 Windows runtime 包先放本地 `_archive/` 存档，不纳入主线打包/发布。跨平台发布等 Windows 主线稳定后再单独规划。

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
- 当前大版本: **3.x.x**
- 大版本号（3）全局统一，小版本号（x.x）各包独立
- 改一个包 → 只 bump 那个包的次版本号（如 3.0.1 → 3.0.2）→ 只推送那个包
- 大版本升级（3.x → 4.0.0）时才全部统一更新
- 不要为了小改动 bump 全部 9 个包

### NuGet 发布流程（单包更新）
改一个包的代码后，按顺序：
1. 改 `<Version>`（如 `3.0.1` → `3.0.2`）
2. `dotnet build <那个项目>.csproj -c Release`
3. `dotnet pack <那个项目>.csproj -c Release -o nupkg/ --no-build`
4. `dotnet nuget push nupkg/<包名>.<新版>.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
5. `gh release create v<新版> nupkg/<包名>.<新版>.nupkg --title "v<新版>" --notes "<改动说明>" --latest`
6. `git add -A && git commit -m "release: <包名> v<新版> — <改动摘要>" && git push`

### OCR runtime 发布流程
本地 OCR 禁止 Python/pip/外部 OCR 执行文件。heavy Paddle/OpenCV native runtime 通过第一方 `Angri450.Nong.OcrRuntime.*` 包分平台部署。

当前发布边界：

- 只发布 `Angri450.Nong.OcrRuntime.WinX64`
- `LinuxX64`、`LinuxArm64`、`OsxX64`、`OsxArm64` 只允许生成到本地 `_archive/`，不推 NuGet
- 下一次整理时，把本地 `nupkg/Angri450.Nong.OcrRuntime.{LinuxX64,LinuxArm64,OsxX64,OsxArm64}.*.nupkg` 挪到 `_archive/`，并确保 `.gitignore` 覆盖归档目录

发布步骤：

1. 只打 Windows runtime 包，或打完整包后立即把非 Windows 包移到 `_archive/`
2. 先推 `Angri450.Nong.OcrRuntime.WinX64` 到 NuGet.org
3. 再推 `Angri450.Nong.Cli`
4. 等华为 NuGet 镜像同步
5. 用华为源验证：`nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json`

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
