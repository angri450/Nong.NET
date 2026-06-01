# CLAUDE.md — Nong.NET

Pure .NET scientific document generation toolkit. Zero JavaScript. One merged foundation DLL.

## 仓库

- GitHub: `https://github.com/angri450/Nong.NET`
- 主分支: `master`（同时维护 `main`）
- 协议: MIT

## 包结构（9 个包，版本号全局统一）

| 包 | 项目路径 | 说明 |
|----|---------|------|
| `Angri450.Nong.ThirdParty` | `ThirdParty/` | **地基** — 合入 15 个第三方库源码编译为单一 DLL |
| `Angri450.Nong.Excel` | `Excel/` | 链式 Excel 生成 API |
| `Angri450.Nong.Chart` | `Chart/` | 18 种图表 + ANOVA/Duncan MRT |
| `Angri450.Nong.Diagram` | `Diagram/` | 流程图、网络图、系统发育树 |
| `Angri450.Nong.Docx` | `Docx/` | Word 生成 + 论文诊断 |
| `Angri450.Nong.Pptx` | `Pptx/` | PPT 生成、10 套主题 |
| `Angri450.Nong.MultiModal` | `MultiModal/` | PaddleOCR 云 + 本地 |
| `Angri450.Nong.Bioicons` | `Bioicons/` | 40 个 SVG 科学图标 |
| `Angri450.Nong.Skill.Manager` | `SkillManager/` | Skill CLI 工具 |

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
- 不要自动切到 main 或 merge main

## 禁止事项
- 不要引入 JavaScript 依赖
- 不要用 Python 实现核心功能（仅 `MultiModal/scripts/ocr_local.py` 为辅助脚本）
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
