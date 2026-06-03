# Agent Contract for nong CLI v3.1.x

## Quick Start

```bash
dotnet tool install --global Angri450.Nong.Cli
nong commands --json       # discover all commands
nong word read file.docx   # extract text
```

## Command Discovery

`nong commands --json` returns all available commands with aliases.
Use this first in any session to know what's available.

## Input Formats

| Command | Expected Input |
|---------|---------------|
| `word read/preview/rebuild` | .docx |
| `word fill` | template .docx + data .json |
| `inspect diagnose/refs/structure` | .txt (plain text) |
| `inspect write-paper` | spec .json |
| `chart analyze/anova/duncan/bar` | groups .json (see below) |
| `chart bar` | groups .json + output .png |
| `excel sheets/read/to-groups` | .xlsx |
| `diagram flowchart/network` | spec .json |
| `genre list/show` | N/A |
| `icons list/search` | N/A |

## Groups JSON Format

```json
{
  "GroupA": [1.2, 1.3, 1.1],
  "GroupB": [2.0, 2.2, 2.1]
}
```

This is the output of `nong excel to-groups`.

## JSON Output Schema

Every command with `--json` returns:

```json
{
  "status": "ok" | "error",
  "command": "word read",
  "summary": "Extracted 29 paragraphs",
  "data": {},
  "issues": [],
  "artifacts": { "docx": "out.docx" },
  "metrics": { "paragraphs": 29 },
  "errors": [],
  "meta": { "durationMs": 42, "version": "3.1.0" }
}
```

## Error Codes

| Code | Name | Meaning |
|------|------|---------|
| E001 | file_not_found | File does not exist |
| E002 | unsupported_format | Wrong file extension |
| E003 | missing_argument | Required argument missing |
| E004 | internal_error | Unexpected error |
| E005 | dependency_missing | Required tool not installed |
| E006 | validation_failed | Content check failed |
| E007 | read_failed | Could not read document |
| E008 | write_failed | Could not write output |

## Common Workflows

### Read a docx
```
nong word read paper.docx
→ pure text to stdout
```

### Diagnose a paper
```
nong inspect diagnose paper.txt --json
→ Read: data.paperType, data.gapGrade, data.evidence[*].adequate
```

### Excel → Statistics → Chart
```
nong excel to-groups data.xlsx --group A --value B --json > groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json
→ Read: artifacts.png for the generated chart
```

### Generate a paper from spec
```
nong inspect write-paper spec.json -o paper.docx --json
→ Read: artifacts.docx
```

## Failure Handling

1. Check `status` field — "error" means the command failed.
2. Read `errors[0].code` for the error code.
3. Read `errors[0].message` for human-readable description.
4. Common fixes:
   - E001: Check file path, use absolute paths.
   - E002: Ensure file extension matches expected format.
   - E005: Run `dotnet tool install --global Angri450.Nong.Cli`.
