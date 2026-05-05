# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

---

# Vid2Sub Project-Specific Guidelines

The following rules are the project-specific constraints for Vid2Sub. They complement the general behavioral guidelines above.

## 1. Project Scope And Technical Boundaries

Vid2Sub is a cross-platform, environment-adaptive CLI tool that converts video/audio files into subtitles. The core transcription engine is Whisper.net.

- Runtime target: `.NET 10`
- Language version: `C# 14`
- Primary use case: local video/audio transcription with `srt`, `vtt`, and `text` output
- Configuration precedence: command-line arguments > YAML configuration file > defaults
- External dependencies: FFmpeg, Whisper.net, YamlDotNet, System.CommandLine

Use C# 14 features when they improve readability. Primary constructors, collection expressions, and similar syntax are welcome when they fit the surrounding code, but do not force new syntax at the cost of clarity or consistency.

## 2. Architecture Rules

Follow the existing layered structure and dependency direction:

- `src/Domain`: domain models, enums, and interfaces. It must not depend on FFmpeg, file-system access, processes, networking, or Whisper native runtime details.
- `src/Application`: workflow orchestration. It coordinates domain interfaces and must not contain platform probing, process invocation, disk IO details, or native runtime details.
- `src/Infrastructure`: infrastructure implementations. This includes FFmpeg, model download/storage, YAML configuration, subtitle writing, disk IO, and Whisper runtime adapters.
- `src/CLI`: command-line parsing, user-facing output, exit codes, and dependency composition.

Design requirements:

- SRP: each class should have one clear responsibility. Audio conversion, model provisioning, subtitle writing, and transcription orchestration should remain separate concerns.
- DIP: Application and Domain code should depend on interfaces, not concrete Infrastructure implementations.
- Add abstractions only when they reduce real complexity or match an existing local pattern.
- Do not perform unrelated refactors, formatting churn, or opportunistic cleanup.

## 3. Whisper.net And Hardware Acceleration

Hardware acceleration must be handled through Whisper.net runtime packages and their autoprobe behavior, not through handwritten OS/GPU detection in business logic.

Runtime package coverage targets:

- Windows: keep `Whisper.net.Runtime.Cuda`, `Whisper.net.Runtime.Vulkan`, `Whisper.net.Runtime.OpenVino`, and `Whisper.net.Runtime`.
- macOS: keep `Whisper.net.Runtime.CoreML` and `Whisper.net.Runtime`.
- Linux or no-RID development: use `Whisper.net.Runtime` CPU fallback as the baseline unless Linux GPU support is explicitly added.

Acceleration constraints:

- Windows builds must keep Vulkan support for AMD/Intel integrated GPUs and non-NVIDIA users.
- Do not implement manual `CUDA > Vulkan > CoreML > CPU` branching in Domain or Application code. That priority should come from the runtime packages' automatic probing behavior.
- After initializing `WhisperFactory`, log `factory.RuntimeDescription` where available so the active backend is diagnosable.
- Handle native library load failures, unsupported drivers, GPU memory exhaustion, and runtime initialization failures with understandable errors.
- If CPU fallback is needed, encapsulate it in the Infrastructure-level Whisper runtime/model provider layer instead of leaking it into business workflow code.

Resource lifetime requirements:

- `WhisperFactory`, `WhisperProcessor`, model streams, facade handles, and other native/unmanaged resources must be released promptly.
- Prefer `using`, `await using`, or explicit scoped lifetimes.
- Do not keep unnecessary native resources alive after async streams or long-running transcription work completes.

## 4. FFmpeg Audio Pipeline

Audio preprocessing belongs in Infrastructure, not Domain or Application.

Implementation requirements:

- Invoke FFmpeg directly through `System.Diagnostics.Process`.
- Prefer `ProcessStartInfo.ArgumentList` over shell command strings so paths with spaces and escaping are handled safely.
- Convert audio to 16 kHz, mono, PCM WAV.
- The command semantics must be equivalent to:

```text
-i {input} -ar 16000 -ac 1 -c:a pcm_s16le -f wav {output}
```

Lifecycle requirements:

- Temporary WAV files must be cleaned up on success, failure, and cancellation.
- Any object that owns temporary files should implement `IDisposable` or `IAsyncDisposable`.
- Capture FFmpeg stderr for diagnostics, but avoid noisy default output.
- Wait for the process asynchronously; do not use sync-over-async blocking.

## 5. Configuration And CLI

Configuration merging must be centralized and testable. Do not scatter precedence rules across multiple layers.

Requirements:

- Command-line arguments have the highest priority.
- YAML configuration is next.
- Code defaults are the final fallback.
- Continue using `System.CommandLine` for CLI parsing.
- Continue using `YamlDotNet` for YAML loading.
- Configuration models should stay in Domain or the existing configuration model location. Configuration loading implementations belong in Infrastructure.

When adding a configuration option, update or consider:

- Default value
- YAML field name
- CLI option name
- README or user documentation
- Test coverage

## 6. Async, Cancellation, And Error Handling

- Use `async`/`await` for file IO, model download, FFmpeg waiting, and long-running transcription work.
- Pass `CancellationToken` through new long-running APIs.
- Error messages should include enough context to identify the failed stage and the relevant input file or model.
- User-facing errors should be clear. Detailed native or FFmpeg output can be kept behind debug log level or exception details.
- Do not swallow exceptions or return vague `false` values in place of diagnosable failures.

## 7. Verification

Before calling a code change complete, run the narrowest meaningful verification for the change.

Common checks:

```bash
dotnet build
dotnet test
```

For changes involving publishing, runtime packages, or native library layout, add the relevant publish check, for example:

```bash
dotnet publish -c Release -r osx-arm64 --self-contained
dotnet publish -c Release -r win-x64 --self-contained
```

If verification cannot be run because of network access, FFmpeg, hardware, runtime packs, or platform limitations, state exactly what was not verified and why.

## 8. Documentation And Maintenance

- This section describes project-specific rules; do not duplicate general LLM behavior guidelines here.
- When the actual `.csproj`, source layout, supported platforms, configuration fields, or CLI options change, update this section, `AGENTS.md`, and README together.
- Keep these instructions tool-neutral. Avoid editor- or agent-specific wording such as "Cursor Agent instructions".
