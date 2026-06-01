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
- 全部 9 个包使用**同一版本号**（当前 3.0.1）
- 发版时：改所有 `*.csproj` 的 `<Version>` → `dotnet build -c Release` → `dotnet pack` → `dotnet nuget push` → `gh release create`
- GitHub Release 和 NuGet 版本同步

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
- `master` / `main` 保持同步
- 发版前合并到 main：`git checkout main && git merge master && git push`

## 禁止事项
- 不要引入 JavaScript 依赖
- 不要用 Python 实现核心功能（仅 `MultiModal/scripts/ocr_local.py` 为辅助脚本）
- 不要为第三方库创建独立的 `.csproj` — 已全部删掉，统一走 ThirdParty
- 不要用 PowerShell 的 `-replace` 批量编辑 `.csproj` — 会损坏 XML，用 Edit 工具逐文件改
