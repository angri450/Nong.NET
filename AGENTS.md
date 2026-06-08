# Agent Development Rules

This file governs the whole repository.

## Working Style

- Move fast. If the goal is clear, inspect the local context and start building.
- Do not turn normal implementation work into an approval workflow.
- Ask only when the next action is destructive, requires credentials, publishes externally, or the codebase gives two genuinely incompatible paths.
- Prefer concrete output over long planning text.

## Required Loop

1. Read guidance first.
   - Start with `CLAUDE.md`.
   - Check the latest relevant files in `log/guidance/`.
   - Read module docs near the work area, such as `Docx/README.md`, `Pdf/README.md`, or `Cli/AGENT.md`.
   - Do not bulk-read the whole repo when a targeted scan is enough.

2. Build the change.
   - Follow existing project patterns.
   - Keep the edit focused, but do not underbuild a feature just to stay small.
   - For bugs, turn the failing file or scenario into a regression asset when practical.
   - For CLI behavior, prefer command-level tests and JSON-contract checks.

3. Verify.
   - Run the narrowest useful build/test command first.
   - For document formatting work, validation alone is not enough; check visual-format evidence in OOXML or generated artifacts.
   - Report any tests that could not be run.

4. Write changelog last.
   - Add a short entry under `log/changelog/`.
   - Use `YYYY-MM-DD-short-topic.md`.
   - Include: what changed, why, files touched, tests run, and remaining risks if any.

## Project Bias

- This repo is aggressive: prefer shipping a working implementation plus regression coverage over leaving a proposal.
- Pure .NET paths are preferred. Do not add JavaScript or Python for core functionality.
- For Word/PDF repair and formatting, prefer OpenXML and Nong commands over COM or helper libraries unless the task explicitly calls for a boundary conversion.
- Existing dirty worktree changes may belong to the user. Work with them; do not revert unrelated files.
