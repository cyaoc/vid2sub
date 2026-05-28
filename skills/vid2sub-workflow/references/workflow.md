# Vid2Sub Codex Workflow

```text
User
  -> confirms media/workbook/output dir
  -> Codex Skill
     -> inspect_workbook
     -> read_glossary
     -> run_vid2sub
     -> parse_subtitles
     -> validate_segments
     -> write_subtitles
     -> manifest + audit
```

## Confirmation Checklist

Before writes, list:

- output directory
- raw Japanese subtitle path
- corrected Japanese subtitle path
- translated Chinese subtitle path
- manifest path
- audit path
- whether existing files will be overwritten

## Failure Handling

- `file_not_found`: ask the user for the corrected path.
- `path_outside_scope`: ask the user to choose an output path under the confirmed output directory.
- `overwrite_not_confirmed`: ask whether to overwrite or choose another output directory.
- `invalid_workbook`: show the sheet/column problem and re-run `inspect_workbook` if needed.
- `unsupported_format`: ask for SRT/VTT input or regenerate raw subtitles as SRT.
- `process_failed`: show the `vid2sub` stage and stderr summary.
