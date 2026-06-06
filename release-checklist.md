# Release Checklist — nong CLI v3.2.4

Date: 2026-06-06

## Build

- [x] `dotnet build Cli/NongCli.csproj -c Release` — 0 errors
- [x] `dotnet test Cli.Tests/Cli.Tests.csproj -c Release` — 81 PASS, 0 SKIP
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File OcrRuntime/pack-runtimes.ps1` — generates and validates 5 first-party OCR runtime packages

## Contract

- [x] `nong commands --json` returns 77 implemented commands
- [x] `nong commands --all --json` returns 77 total (77 impl)
- [x] All real commands return stable JSON schema
- [x] Error codes E001-E009 documented
- [x] AGENT.md written
- [x] CAPABILITY.md current

## Implemented (77 commands across 11 groups)

| Group | Count | Commands |
|-------|-------|----------|
| word | 32 | check, convert, read, preview, fill, rebuild, extract, dissect, stats, fonts, styles, validate, merge, outline, images, comments, revisions, infer-format, fix-order, protect, embed-font, add paragraph, add table, add footnote, add endnote, add image, add toc, add xref, add link, add bookmark, add comment, add math |
| inspect | 10 | diagnose, refs, write-paper, classify, structure, varplan, evidence, data-req, gap, semantics |
| chart | 7 | analyze, bar, anova, duncan, line, scatter, pie |
| excel | 4 | sheets, read, to-groups, create |
| diagram | 3 | flowchart, network, tree |
| genre | 2 | list, show |
| icons | 2 | list, search |
| skill | 4 | validate, scan, inventory, package |
| pptx | 2 | read, slides |
| ocr | 7 | cloud, local, check-env, analyze-image, models, install-model, to-word |
| pdf | 4 | check, dissect, render, images |

`ocr local` is implemented through pure .NET PP-OCRv5 (`Sdcb.PaddleOCR`) and returns E005 when the platform native runtime cache is missing or unloadable.
`ocr install-model pp-ocrv5-mobile` installs/checks the current-platform first-party `Angri450.Nong.OcrRuntime.*` bundle from Huawei NuGet/cache. Upstream Sdcb/OpenCvSharp fallback is disabled by default and requires explicit `--allow-upstream-fallback`; `--dry-run` reports the plan without installing.

## Known Limitations

- word merge: uses AdvancedFeatures.AppendDocument and returns warnings for headers/footers, numbering, and style-name conflict boundaries
- ocr cloud: markdown output only; PADDLEOCR_ACCESS_TOKEN required (PADDLEOCR_TOKEN deprecated)
- ocr local: Windows x64 smoke test passes; Linux/macOS runtime bundles are packaged but must be smoke-tested on those platforms before being called stable
- ocr install-model: may require network access to a NuGet v3 source for `Angri450.Nong.OcrRuntime.*`; default source is Huawei Cloud NuGet v3
- pdf local OCR mode is text-recognition only; cloud OCR remains the stronger path for layout-heavy/table-heavy PDF reconstruction
- Duncan MRT: simplified Q-value approximation; formal papers should verify
- word rebuild: input/output must differ
- word fix-order / protect / embed-font / add: input/output must differ

## Skill Manager Legacy

- [x] `dotnet build SkillManager/SkillManager.Cli.csproj -c Release` — 0 errors (legacy only)
- [ ] Do NOT pack or publish `Angri450.Nong.Skill.Manager` as a public global tool in the CLI-first line
- [x] Only `Angri450.Nong.Cli` is the public tool entry point
- [x] Users should use `nong skill validate/scan/inventory/package` instead of `skill-manager`

## NuGet Publish

- [x] Push OCR runtime packages first:
  - `dotnet nuget push nupkg/Angri450.Nong.OcrRuntime.WinX64.3.2.4.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
  - `dotnet nuget push nupkg/Angri450.Nong.OcrRuntime.LinuxX64.3.2.4.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
  - `dotnet nuget push nupkg/Angri450.Nong.OcrRuntime.LinuxArm64.3.2.4.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
  - `dotnet nuget push nupkg/Angri450.Nong.OcrRuntime.OsxX64.3.2.4.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
  - `dotnet nuget push nupkg/Angri450.Nong.OcrRuntime.OsxArm64.3.2.4.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
- [x] Push PDF package: `dotnet nuget push nupkg/Angri450.Nong.Pdf.3.2.4.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
- [x] Push CLI after runtime and PDF packages: `dotnet nuget push nupkg/Angri450.Nong.Cli.3.2.4.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json`
- [ ] Wait for Huawei mirror sync, then verify: `nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json`
- [ ] GitHub Release: `gh release create v3.2.4-cli nupkg/Angri450.Nong.OcrRuntime.*.3.2.4.nupkg nupkg/Angri450.Nong.Pdf.3.2.4.nupkg nupkg/Angri450.Nong.Cli.3.2.4.nupkg`
