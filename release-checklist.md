# Release Checklist — nong CLI v3.1.x

Date: 2026-06-03

## Build

- [x] `dotnet build Cli/NongCli.csproj -c Release` — 0 errors
- [x] `dotnet build ThirdParty/ThirdParty.csproj -c Release` — 0 errors
- [x] `dotnet build Docx/DocxCore.csproj -c Release` — 0 errors
- [x] `dotnet build Inspect/Inspect.csproj -c Release` — 0 errors

## Pack

- [x] `dotnet pack Cli/NongCli.csproj -c Release -o nupkg/` — success
- [x] nupkg: Angri450.Nong.Cli.3.1.0.nupkg

## Local Install

- [ ] `dotnet tool install --global --add-source nupkg/ Angri450.Nong.Cli`
- [ ] `nong --version` → 3.1.0
- [ ] `nong commands --json` → valid JSON
- [ ] `nong word read test.docx` → text output

## Contract

- [x] `nong commands --json` returns 40+ commands with aliases
- [x] All real commands return stable JSON schema
- [x] Error codes E001-E008 documented
- [x] AGENT.md written

## NuGet Publish (pending)

- [ ] Push to NuGet.org: `dotnet nuget push nupkg/Angri450.Nong.Cli.3.1.0.nupkg --api-key $env:NUGET_API_KEY`
- [ ] GitHub Release: `gh release create v3.1.0-cli nupkg/Angri450.Nong.Cli.3.1.0.nupkg`

## License

- [ ] ThirdParty source list audit
- [ ] NOTICE file for Apache 2.0
- [ ] Verify no GPL code merged

## Real Commands Implemented

| Command | Stage |
|---------|-------|
| nong word read | 2 |
| nong word preview | 2 |
| nong word fill | 6 |
| nong word rebuild | 6 |
| nong inspect diagnose | 3 |
| nong inspect refs | 3 |
| nong inspect write-paper | 6 |
| nong chart analyze | 4 |
| nong chart anova | 3 |
| nong chart duncan | 3 |
| nong chart bar | 4 |
| nong excel sheets | 5 |
| nong excel read | 5 |
| nong excel to-groups | 5 |
| nong diagram flowchart | 7 |
| nong diagram network | 7 |
| nong genre list | 8 |
| nong genre show | 8 |
| nong icons list | 8 |
| nong icons search | 8 |

**20 real commands across 8 groups.**

Note: pptx read is a stub (not counted). PptxCore needs further adaptation.

Naming: `inspect write-paper` (with hyphen) is the canonical name, consistent with AGENT.md and commands --json.
