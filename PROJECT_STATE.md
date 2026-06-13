# Nong.Cli.Net Project State

Last updated: 2026-06-13

This file is the current truth source for agents. Read it before `CLAUDE.md`, `AGENTS.md`, `README.md`, or any file under `log/`.

## Current Work

Active plan/handoff:

- `log/plans/2026-06-13-4.1.0-release-handoff.md`

Current immediate objective:

- Sync the 4.1.0 modular release truth sources: `CliVersion`, English README, agent docs, capability/release docs, and package output workflow.

Do not infer current work from older `log/plans/*` files unless this file or the active handoff links to them.

## Planning Workflow

Development plans live in `log/plans/`.

Only the plan linked above is active for a builder window. Older plans remain as history and must not be scanned to infer current work.

Two-window workflow:

- Planner window: reads history, writes or updates `log/plans/YYYY-MM-DD-topic.md`, updates `log/plans/index.md`, then updates this file's active plan pointer.
- Builder window: reads this file and the active plan only, then implements and verifies.

Detailed policy:

- `docs/wiki/planning-workflow.md`
- `log/plans/README.md`

## Current Architecture

Nong.Cli.Net is a pure .NET document and research CLI toolkit.

The current 4.1.0 line uses a modular tool architecture:

- `nong` is a light router plus pure .NET command groups.
- Heavy command groups are external dotnet tools.
- User command names stay stable, for example `nong chart bar ...`.

Main CLI package:

- `Cli/NongCli.csproj`
- Package: `Angri450.Nong.Cli`
- Tool command: `nong`
- Version line: `4.1.0`

External tool packages:

- `Angri450.Nong.Tool.Chart` -> `nong-chart`
- `Angri450.Nong.Tool.Diagram` -> `nong-diagram`
- `Angri450.Nong.Tool.Pdf` -> `nong-pdf`
- `Angri450.Nong.Tool.Pptx` -> `nong-pptx`
- `Angri450.Nong.Tool.Ocr` -> `nong-ocr`
- `Angri450.Nong.Tool.Imaging` -> `nong-imaging`

The main CLI still owns the light command groups:

- `word`
- `excel`
- `inspect`
- `lit`
- `genre`
- `icons`
- `slice`
- `skill`
- `progress`

## Current Command Surface

Current local command discovery:

```powershell
.\Cli\bin\Release\net8.0\nong.exe commands --json
.\Cli\bin\Release\net8.0\nong.exe commands --format openai-tools
```

Expected command count:

- `125 commands available`
- `125` OpenAI tool schemas

If command count or version metadata disagrees with this file, treat that as a current-state drift to investigate.

## OCR Contract

Active local OCR line:

- PP-OCRv6 first.
- `pp-ocrv6-medium` is the default local install target.
- `pp-ocrv5-mobile` remains a legacy compatibility path.
- No Python, pip, or external OCR executable for local OCR core functionality.

Native OCR runtime contract:

- Runtime packages live in sibling repo `Nong.OcrRuntime`.
- NuGet package prefix remains `Angri450.Nong.OcrRuntime.*`.
- `Cli/Common/OcrRuntimeVersion.cs` is intentionally independent from CLI version.
- Do not bump `OcrRuntimeVersion.Current` unless the sibling runtime repo has published a validated native runtime.

## Current Risks

These are active risks, not historical notes:

- `Cli/Common/CliVersion.cs` still reports `4.0.0` while project files are `4.1.0`.
- `README.md`, `CLAUDE.md`, `Cli/AGENT.md`, `docs/CAPABILITY.md`, and `docs/release-checklist.md` contain stale 4.0.0 / PP-OCRv5-only wording.
- Root `nupkg/` contains stale package outputs and must not be used as a publish source until refreshed from current project files.

## Information Sources

Use this order:

1. `PROJECT_STATE.md` for current truth.
2. `CLAUDE.md` and `AGENTS.md` for agent behavior.
3. The active handoff or active plan linked above.
4. `docs/wiki/` for stable project knowledge, including `docs/wiki/planning-workflow.md`.
5. `log/` only as historical evidence.

Never bulk-read `log/` to decide current state. Read only files linked from this file, the active plan, or a specific task.

## Verification Baseline

Known-good local verification from the latest audit:

```powershell
dotnet test Cli.Tests\Cli.Tests.csproj -c Release --no-restore
```

Expected:

```text
154 passed, 0 failed, 0 skipped
```

Current-project pack audit must use a clean output directory, not root `nupkg/`.
