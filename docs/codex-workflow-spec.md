# Vid2Sub Codex 字幕工作流规格书

## 1. 背景与目标

Vid2Sub 现有 exe 负责从视频或音频生成字幕。新功能不改动核心转录引擎，而是在其上层增加一个 Codex 工作流产品。

目标流程：

1. 用户提供视频或音频文件。
2. Codex 调用已编译的 `vid2sub` exe 生成原始字幕。
3. 用户提供 Excel 校正或翻译手册。
4. Codex 读取手册，校正日文字幕。
5. Codex 根据校正后的日文字幕和翻译手册生成中文字幕。
6. 用户可要求调整字幕时长、切分、合并。
7. 输出可交付字幕文件：原始日文、校正日文、中文字幕。

## 2. 架构选择

采用 **Codex Skill + 本地 MCP Server**。

- Skill 负责用户交互和流程编排。
- MCP Server 负责稳定、可测试的本地工具调用，首版使用 C# / .NET 10 sidecar。
- `vid2sub` exe 继续作为转录引擎。
- 不把 Excel、LLM 校正、翻译逻辑塞进 Vid2Sub 核心 CLI。

理由：

- Codex skill 适合封装可复用工作流。
- MCP 适合把本地 exe、Excel、字幕读写能力暴露成工具。
- C# sidecar 能复用现有测试体系和 Infrastructure 字幕能力。
- `AGENTS.md` 不是核心路由机制，只作为项目开发说明或可选提示。

参考：

- Codex Skills: https://developers.openai.com/codex/skills
- Codex MCP: https://developers.openai.com/codex/mcp
- AGENTS.md: https://developers.openai.com/codex/guides/agents-md
- MCP: https://modelcontextprotocol.io/docs/getting-started/intro

## 3. 用户入口

第一版使用 **手动优先**。

用户通过以下方式启动：

- 显式调用 skill，例如 `$vid2sub-workflow`
- 或直接说：“帮我用这个视频和这个 Excel 生成日文校正版和中文字幕”

不做静默自动执行。涉及读写本地文件、运行 exe、生成字幕时，Codex 必须先明确当前计划和输出路径。

## 4. Skill 规格

创建 skill：`vid2sub-workflow`

目录结构：

```text
vid2sub-workflow/
  SKILL.md
  references/
    workflow.md
    glossary-guidelines.md
  assets/
    sample-config.yaml
```

`SKILL.md` 职责：

- 收集缺失输入：视频路径、Excel 路径、源语言、目标语言、输出目录。
- 调用 MCP 工具生成原始字幕。
- 调用 MCP 工具读取 Excel 结构。
- 当 Excel 列名或工作表不明确时，让用户确认。
- 指导 Codex 校正日文字幕。
- 指导 Codex 翻译中文字幕。
- 支持用户后续要求调整字幕切分和时长。
- 在写文件前说明将生成或覆盖哪些文件。

## 5. MCP Server 规格

新增本地 MCP server，例如：`vid2sub-workflow-mcp`

首版工具如下。

### `run_vid2sub`

调用已编译 Vid2Sub exe。

输入：

```json
{
  "executable_path": "string",
  "input_path": "string",
  "output_root": "string",
  "output_path": "string",
  "format": "srt | vtt | text",
  "language": "string | null",
  "model": "string | null",
  "overwrite_confirmed": "boolean"
}
```

输出：

```json
{
  "status": "success | failed",
  "data": {
    "subtitle_path": "string | null",
    "stderr": "string | null"
  },
  "code": "string | null",
  "message": "string | null",
  "details": "string | null",
  "warnings": []
}
```

要求：

- 使用参数数组调用进程，不拼 shell 字符串。
- 捕获 stderr。
- 不吞异常。
- 路径含空格必须正常工作。
- 通过 MCP 工具层强制输出目录 scope 和覆盖确认。

### `inspect_workbook`

读取 Excel 文件结构。

输入：

```json
{
  "path": "string"
}
```

输出：

```json
{
  "sheets": [
    {
      "name": "string",
      "columns": ["string"],
      "sample_rows": [{}]
    }
  ]
}
```

要求：

- 不要求固定模板。
- 只返回少量 sample rows，避免把整个 Excel 塞进上下文。
- Codex 根据结构和用户意图判断日文 key、中文翻译、备注列。

### `read_glossary`

读取用户确认后的手册内容。

输入：

```json
{
  "path": "string",
  "sheet": "string",
  "key_column": "string",
  "translation_column": "string",
  "notes_column": "string | null"
}
```

输出：

```json
{
  "entries": [
    {
      "key": "string",
      "translation": "string",
      "notes": "string | null"
    }
  ],
  "warnings": ["string"]
}
```

要求：

- 空 key 跳过并警告。
- 重复 key 警告。
- 不在工具里做翻译判断。

### `parse_subtitles`

把字幕文件解析成结构化 segments。

输入：

```json
{
  "path": "string"
}
```

输出：

```json
{
  "format": "srt | vtt | text",
  "segments": [
    {
      "index": 1,
      "start": "00:00:01.000",
      "end": "00:00:03.500",
      "text": "string"
    }
  ]
}
```

### `validate_segments`

检查字幕切分合法性。

检查项：

- `start < end`
- 时间单调递增
- 无重叠
- 无空文本
- 单条字幕时长过长时给 warning
- 字幕间隔异常时给 warning

### `write_subtitles`

写出字幕文件。

输入：

```json
{
  "path": "string",
  "output_root": "string",
  "format": "srt | vtt",
  "segments": [],
  "overwrite_confirmed": "boolean"
}
```

要求：

- 写入前先调用 `validate_segments`。
- 默认不覆盖，除非用户明确确认。
- 输出 UTF-8。
- 拒绝写入确认输出目录以外的路径。

### 统一工具返回

所有 MCP 工具统一返回：

```json
{
  "status": "success | failed",
  "data": {},
  "code": "string | null",
  "message": "string | null",
  "details": "string | null",
  "warnings": [
    {
      "code": "string",
      "message": "string",
      "details": "string | null"
    }
  ]
}
```

错误码必须稳定，供 Skill 展示可恢复的用户提示。

## 6. 工作流细节

标准流程：

1. **准备输入**
   - 用户提供视频或音频路径。
   - 用户提供 Excel 路径。
   - 用户说明目标，例如“日文校正后翻译成中文”。
2. **生成原始字幕**
   - Codex 调用 `run_vid2sub`。
   - 输出：`{inputName}.raw.ja.srt`
3. **读取 Excel**
   - 调用 `inspect_workbook`。
   - Codex 判断可能的 key 或 translation 列。
   - 如果不明确，询问用户确认。
   - 调用 `read_glossary`。
4. **校正日文字幕**
   - 调用 `parse_subtitles({inputName}.raw.ja.srt)`。
   - Codex 对照 glossary 校正文本。
   - 保持原时间轴不变。
   - 输出：`{inputName}.corrected.ja.srt`
5. **翻译中文字幕**
   - 使用 `corrected.ja.srt` 作为源。
   - 使用 glossary 作为术语约束。
   - 输出：`{inputName}.translated.zh.srt`
6. **用户调整切分**
   - 用户可以说：
     - “每条不要超过 3 秒”
     - “这两句合并”
     - “这里切得太碎”
     - “中文和日文时间轴保持一致”
   - Codex 修改结构化 segments。
   - 调用 `validate_segments`。
   - 重新写出日文和中文字幕。
7. **写出交付证据**
   - 输出 `workflow.manifest.json`。
   - 输出 `glossary-audit.json`，记录匹配、未匹配、重复和警告。

## 7. 非目标

首版不做：

- 不改 Vid2Sub 核心转录架构。
- 不做 GUI。
- 不做云端队列。
- 不做多人协作。
- 不做自动监听目录。
- 不训练模型。
- 不强制 Excel 模板。
- 首版不做双语字幕文件。
- 首版不做 Excel/字幕大小上限或 `run_vid2sub` timeout。

这些可以作为后续产品化阶段考虑。

## 8. 验收标准

功能验收：

- 能用一个视频生成原始日文字幕。
- 能读取用户指定 Excel。
- 能让用户确认 Excel 映射。
- 能生成校正日文字幕。
- 能生成中文字幕。
- 能根据用户要求调整字幕切分。
- 输出字幕时间轴合法。

工程验收：

- MCP 工具有单元测试。
- 路径含空格测试通过。
- Excel 多 sheet 测试通过。
- SRT parse/write roundtrip 测试通过。
- 非法时间轴会被拒绝或警告。
- exe 调用失败时错误可读。
- MCP server publish/install smoke test 通过。
- glossary audit 和 workflow manifest 写出测试通过。
- Skill eval 覆盖术语遵守、时间轴不变和未匹配报告。

## 9. 推荐实施顺序

1. 实现 MCP Server 骨架。
2. 实现 `parse_subtitles`、`write_subtitles`、`validate_segments`。
3. 实现 `inspect_workbook`、`read_glossary`。
4. 实现 `run_vid2sub`。
5. 实现 workflow manifest 和 glossary audit。
6. 编写 `vid2sub-workflow` skill。
7. 补充安装说明和 MCP 配置 smoke test。
8. 用一个真实样例跑通端到端流程。

## 10. 待 Review 问题

- MCP server 使用 C# / .NET 10 sidecar。
- Excel 读取库使用 ClosedXML，首版支持 `.xlsx`。
- 输出命名使用 `{inputName}.{stage}.{language}.srt`。
- 双语字幕文件放入 TODO，不进首版。
- glossary 匹配结果写入 `glossary-audit.json`。

## 11. 安装与分发

构建 MCP server：

```bash
dotnet publish tools/Vid2Sub.WorkflowMcp/Vid2Sub.WorkflowMcp.csproj -c Release
```

构建 Vid2Sub CLI：

```bash
dotnet publish -c Release
```

Codex MCP 配置使用本地 stdio server，指向已发布的 `vid2sub-workflow-mcp`。详见 `docs/vid2sub-workflow-install.md`。
