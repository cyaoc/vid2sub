# Vid2Sub Codex Workflow Install

This workflow adds a local MCP server and a Codex skill on top of the existing `vid2sub` CLI.

## Build

```bash
dotnet publish tools/Vid2Sub.WorkflowMcp/Vid2Sub.WorkflowMcp.csproj -c Release
dotnet publish -c Release
```

The MCP server output is:

```text
tools/Vid2Sub.WorkflowMcp/bin/Release/net10.0/publish/vid2sub-workflow-mcp
```

The `vid2sub` CLI output is:

```text
bin/Release/net10.0/publish/vid2sub
```

## Codex MCP Configuration

Add the MCP server as a local stdio server in your Codex config. Use absolute paths in real configuration.

```toml
[mcp_servers.vid2sub-workflow]
command = "/absolute/path/to/vid2sub-workflow-mcp"
args = []
```

## Skill Installation

Copy or symlink the skill directory into your Codex skills directory:

```text
skills/vid2sub-workflow/
```

Then invoke it explicitly:

```text
$vid2sub-workflow
```

## Smoke Test

1. Start Codex with the MCP configuration.
2. Confirm the MCP server exposes these tools:
   - `run_vid2sub`
   - `inspect_workbook`
   - `read_glossary`
   - `parse_subtitles`
   - `validate_segments`
   - `write_subtitles`
3. Run the skill with one media file and one `.xlsx` glossary.
4. Confirm the output directory contains:
   - `{inputName}.raw.ja.srt`
   - `{inputName}.corrected.ja.srt`
   - `{inputName}.translated.zh.srt`
   - `workflow.manifest.json`
   - `glossary-audit.json`
