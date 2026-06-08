# Release Checklist - Nong.NET v4.0.0

Date: 2026-06-08

## Build And Test

- [x] `dotnet build .\Cli\NongCli.csproj -c Release` - 0 errors
- [x] `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release` - 114 passed
- [x] `dotnet test .\Tests\Tests.csproj -c Release` - 119 passed
- [x] `nong commands --json` reports 93 commands and CLI version `4.0.0`

Native image rendering tests remain opt-in with `NONG_RUN_NATIVE_IMAGE_TESTS=1` because Skia/ScottPlot native cleanup can crash `testhost.exe` on some Windows machines.

## Contract

- [x] All main Nong package versions are `4.0.0`.
- [x] `Cli/Common/CliVersion.cs` reports `4.0.0`.
- [x] README and README.zh-CN document the 4.0.0 release line.
- [x] `Cli/AGENT.md` documents the `v4.0.x` agent contract.
- [x] Word/PDF/Excel/PPT slices use `nong-pandoc/package/v1`.
- [x] `word format-audit` exposes CI gates through `--fail-on-warning` and `--min-score`.

## Mainline Packages

Publish these `4.0.0` packages:

- `Angri450.Nong.ThirdParty`
- `Angri450.Nong.Bioicons`
- `Angri450.Nong.Genre`
- `Angri450.Nong.Pandoc`
- `Angri450.Nong.Docx`
- `Angri450.Nong.Excel`
- `Angri450.Nong.Chart`
- `Angri450.Nong.Diagram`
- `Angri450.Nong.Pdf`
- `Angri450.Nong.Pptx`
- `Angri450.Nong.Literature`
- `Angri450.Nong.MultiModal`
- `Angri450.Nong.Inspect`
- `Angri450.Nong.OcrRuntime.WinX64`
- `Angri450.Nong.Cli`

## OCR Runtime Boundary

- [ ] Push only `Angri450.Nong.OcrRuntime.WinX64.4.0.0.nupkg` as the mainline OCR runtime package.
- [ ] Do not publish Linux/macOS runtime packages in this release unless the maintainer explicitly overrides the current runtime policy.
- [ ] After NuGet and mirror sync, verify:
  `nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json`

## Git And NuGet

- [ ] Commit local release changes: `release: Nong.NET v4.0.0`
- [ ] Push `master` to GitHub `origin`.
- [ ] Push `master` to Gitee.
- [ ] Push `master` to GitCode.
- [ ] Pack all mainline packages into `nupkg/`.
- [ ] Push all `4.0.0` mainline packages to NuGet with `--skip-duplicate`.

## Known Limits

- `word table-reflow` still uses explicit row/column thresholds and does not yet detect real Word pagination overflow.
- Local OCR runtime stability is Windows x64 mainline; other platforms require target-machine smoke tests before stable claims.
- PDF scan-mode quality depends on local OCR runtime availability.
