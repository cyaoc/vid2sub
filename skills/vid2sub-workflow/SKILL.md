# Vid2Sub Workflow

Use this skill when the user wants to turn a video/audio file plus an Excel glossary into corrected Japanese subtitles and Chinese subtitles.

## Inputs To Collect

- Media path: video or audio file to transcribe.
- Workbook path: `.xlsx` glossary/correction workbook.
- Source language: default `ja` unless the user says otherwise.
- Target language: default `zh`.
- Output directory: ask before writing files.
- `vid2sub` executable path and `vid2sub-workflow-mcp` MCP configuration.

Before calling any write-capable tool, state the exact files that will be created or overwritten and get explicit confirmation.

## Standard Workflow

1. Confirm inputs and output directory.
2. Call `inspect_workbook` for workbook sheets, columns, and sample rows.
3. Ask the user to confirm sheet and columns when the mapping is not obvious.
4. Call `read_glossary` with the confirmed mapping.
5. Call `run_vid2sub` to create `{inputName}.raw.ja.srt`.
6. Call `parse_subtitles` on the raw subtitle.
7. Correct Japanese subtitle text using the glossary. Keep timestamps unchanged.
8. Translate corrected Japanese subtitles into Chinese using glossary terms as hard constraints.
9. Call `validate_segments` before every subtitle write.
10. Call `write_subtitles` for `{inputName}.corrected.ja.srt` and `{inputName}.translated.zh.srt`.
11. Write `workflow.manifest.json` and `glossary-audit.json` in the output directory.

## Output Naming

Use input-prefixed stage names:

- `{inputName}.raw.ja.srt`
- `{inputName}.corrected.ja.srt`
- `{inputName}.translated.zh.srt`
- `workflow.manifest.json`
- `glossary-audit.json`

## Safety Rules

- Do not silently run `vid2sub`.
- Do not write outside the confirmed output directory.
- Do not overwrite without explicit user confirmation.
- Treat MCP tool failures as recoverable user-facing errors when possible.
- If glossary warnings exist, show them before translating.

## Translation Rules

- Preserve subtitle timing unless the user explicitly asks to split, merge, or retime segments.
- Preserve segment count during correction/translation unless the user asks otherwise.
- Glossary translations override model preference.
- Record unmatched, duplicate, and ambiguous glossary entries in the audit output.

See `references/workflow.md` and `references/glossary-guidelines.md` for detailed flow and evaluation cases.
