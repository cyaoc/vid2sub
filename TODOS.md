# TODOS

## Infrastructure

### Exact Model Checksums

**What:** Add exact checksum verification for each supported Whisper model artifact.

**Why:** Basic size validation catches truncated downloads, but exact checksums catch wrong or corrupt artifacts before native Whisper loading.

**Context:** The structure/config/testing review chose atomic model downloads plus nonzero/minimum-size validation for the current refactor. Exact checksums are a future hardening step if release integrity or supply-chain confidence becomes more important. Start in the model provider after the new Vid2Sub-owned model type and Whisper mapping are in place.

**Effort:** M
**Priority:** P2
**Depends on:** Model provider refactor and atomic download path

### Streaming Subtitle Output

**What:** Add temp-file streaming subtitle output for very long media.

**Why:** Buffering all segments and writing once preserves atomic output, but very long media may eventually need lower memory use.

**Context:** The current plan keeps segment buffering so failed transcriptions do not leave partial final subtitle files. A future implementation can stream to a temporary subtitle file and move it to the final path only after successful transcription.

**Effort:** M
**Priority:** P3
**Depends on:** Staged subtitle output writer boundary

### Generic Host Migration Evaluation

**What:** Evaluate migrating CLI composition, configuration, and logging to .NET Generic Host.

**Why:** Generic Host would provide standard .NET composition, options validation, configuration providers, and logging setup if Vid2Sub grows beyond a focused CLI.

**Context:** The CEO review chose the smaller boundary refactor over a full Host migration because current failures are build inclusion, source tracking, config resolution, Domain purity, runtime boundaries, and tests. Revisit this only after the current refactor lands cleanly or if Vid2Sub needs multi-provider, multi-frontend, environment-variable, or service-style configuration.

**Effort:** L
**Priority:** P3
**Depends on:** Current boundary refactor landing cleanly

### Bilingual Subtitle Output

**What:** Generate an optional bilingual subtitle file that combines corrected Japanese and translated Chinese segments.

**Why:** Some subtitle delivery workflows need side-by-side Japanese/Chinese subtitles for review, learning, or publishing.

**Context:** The Codex workflow v1 focuses on raw Japanese, corrected Japanese, translated Chinese, workflow manifest, and glossary audit. Bilingual output was explicitly deferred so the first version can stabilize the single-language timeline and glossary pipeline before adding formatting choices such as line order, separator, and player compatibility.

**Effort:** M
**Priority:** P2
**Depends on:** Stable corrected/translated subtitle segment alignment

### Workflow Large File Limits And Timeouts

**What:** Add configurable limits for Excel samples, returned subtitle segments, and `run_vid2sub` execution time.

**Why:** Very large workbooks, very long subtitle files, or a stuck `vid2sub` process can consume memory, block Codex workflow progress, or flood model context.

**Context:** The engineering review chose not to enforce size or timeout limits in v1. Revisit once real workflow samples show large-file behavior or when the workflow is used outside controlled local files. Start with sample row caps, maximum returned cells, segment count warnings, truncation warnings, and process cancellation.

**Effort:** M
**Priority:** P2
**Depends on:** MCP workflow tools and manifest/audit outputs landing

## Completed
