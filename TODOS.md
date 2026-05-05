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

## Completed
