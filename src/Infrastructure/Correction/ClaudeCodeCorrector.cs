using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;
using Vid2Sub.Domain.Services;

namespace Vid2Sub.Infrastructure.Correction;

/// <summary>
/// Proofreads segments with the locally installed Claude Code CLI (<c>claude -p</c>), which runs on the
/// user's Claude subscription and needs no API key. The model returns only the ranges it changes and
/// <see cref="SegmentEditor"/> computes their timing, so the model never writes a timestamp and cannot
/// alter a segment it did not name. A transcript is corrected as a whole or not at all: any failure throws.
/// The corrections are likely, not certain, so each one comes back with the model's reason for a person to review.
/// </summary>
/// <param name="model">Model ID for <c>claude --model</c>.</param>
/// <param name="effort">Level for <c>claude --effort</c>.</param>
/// <param name="rules">Returns what to change and what to leave alone, in prose, for a language code (null when unknown), and where that came from; see <see cref="CorrectionRules"/>.</param>
/// <param name="glossary">The correct spellings of names and terms, one per line; may be empty.</param>
/// <param name="log">Receives progress.</param>
/// <param name="run">Sends a system prompt and a user message and returns the CLI's JSON result; defaults to running <c>claude</c>.</param>
/// <param name="language">The language the user named for the subtitles; null to go by what Whisper recognized.</param>
public sealed class ClaudeCodeCorrector(
    string model,
    CorrectionEffort effort,
    Func<string?, (string Text, string Source)> rules,
    string glossary,
    Action<string> log,
    Func<string, string, CancellationToken, Task<string>>? run = null,
    string? language = null) : ISubtitleCorrector
{
    // Chunks keep each answer short and limit what one failure costs.
    // shortcut: chunks are cut at a fixed count and sent one after another, each needing the result of the one before; cut at pauses and send in parallel if long recordings need it
    private const int ChunkSize = 150;

    /// <summary>Neighbouring segments shown for reference, so a sentence that crosses a chunk edge still reads whole.</summary>
    private const int ContextSize = 5;

    /// <summary>
    /// Cues at the end of a chunk that the next chunk may edit again. A neighbour shown for reference cannot be
    /// merged with, so without these a sentence or a term that a chunk edge cut in two would stay in two cues.
    /// </summary>
    private const int OverlapSize = 5;

    private static readonly TimeSpan ChunkTimeout = TimeSpan.FromMinutes(10);

    private const string Schema =
        """{"type":"object","properties":{"edits":{"type":"array","items":{"type":"object","properties":{"from":{"type":"integer"},"to":{"type":"integer"},"lines":{"type":"array","items":{"type":"string"}},"kind":{"type":"string","enum":["guess","hallucination","term","segmentation"]}},"required":["from","to","lines","kind"],"additionalProperties":false}}},"required":["edits"],"additionalProperties":false}""";

    /// <summary>
    /// The part of the prompt the code depends on: what the input lines mean and how edits are returned.
    /// What to change is the user's to decide and comes from the rules file.
    /// </summary>
    private const string Protocol =
        """
        You proofread subtitles that Whisper speech recognition produced. People who were not at the talk will read them, so every cue must be correct and easy to read at a glance.

        <rules> says what to change and what to leave alone; follow it and change nothing else. <glossary>, when present, lists the correct spellings of names and terms, one per line.

        The user message holds the cues to proofread inside <segments>, one per line as `number [seconds, width] text`. Both numbers were measured for you: how long the cue is shown, and how wide its text is, counting a full-width character as 1 and a half-width letter or digit as 0.5. <context_before> and <context_after> show the neighbouring cues for reference only; they cannot be edited.

        Return only the ranges you change and leave out cues that are already fine. In each edit, `from` and `to` are the first and last cue numbers it replaces, and `lines` are the cues that take their place, in order (empty to delete them). The new cues share the replaced time span in proportion to their length, so keep each range as small as possible. Ranges must be in ascending order and must not overlap.

        A person reviews your edits afterwards, starting with the ones most likely to be wrong, so give each edit its `kind`, the first of these that applies: `hallucination` when it deletes text you judge the recognizer invented; `guess` when it changes a word on your own judgement of what was said; `term` when it changes a word to a spelling from the glossary; `segmentation` when it only moves cue boundaries and leaves the words as they were.
        """;

    private readonly string _glossary = string.IsNullOrWhiteSpace(glossary) ? "" : $"\n<glossary>\n{glossary.Trim()}\n</glossary>\n";

    /// <summary>
    /// Fails unless the <c>claude</c> command is installed and signed in, so that a run which asked for
    /// correction stops before it transcribes anything rather than after.
    /// </summary>
    /// <param name="status">Returns what <c>claude auth status</c> prints; defaults to running it.</param>
    public static async Task EnsureSignedInAsync(
        Func<CancellationToken, Task<string>>? status = null,
        CancellationToken cancellationToken = default)
    {
        status ??= async token => (await RunAsync(["auth", "status"], "", TimeSpan.FromSeconds(30), token)).Output;
        var json = await status(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("loggedIn", out var loggedIn) && loggedIn.ValueKind == JsonValueKind.False)
            {
                throw new InvalidOperationException("Claude Code is not signed in: run `claude` and sign in, or turn correction off.");
            }
        }
        catch (JsonException)
        {
            // A version that answers differently is not proof of a problem; the first correction will tell.
        }
    }

    private string EffortLevel => effort.ToString().ToLowerInvariant();

    public async Task<(IReadOnlyList<TranscriptionSegment> Segments, IReadOnlyList<SegmentChange> Changes)> CorrectAsync(
        IReadOnlyList<TranscriptionSegment> segments,
        CancellationToken cancellationToken = default)
    {
        // Break points differ by language, so the rules are put together for the language of these subtitles.
        var spoken = language ?? segments.Select(segment => segment.Language).FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));
        var (rulesText, rulesSource) = rules(spoken);
        log($"Correction rules: {rulesSource}");
        var systemPrompt = $"{Protocol}\n\n<rules>\n{rulesText.Trim()}\n</rules>\n{_glossary}";

        var send = run ?? RunClaudeAsync;
        var corrected = new List<TranscriptionSegment>(segments.Count);
        var changes = new List<SegmentChange>();
        for (var first = 0; first < segments.Count; first += ChunkSize)
        {
            var end = Math.Min(first + ChunkSize, segments.Count);
            var carried = Math.Min(OverlapSize, corrected.Count);
            List<TranscriptionSegment> chunk = [.. corrected.TakeLast(carried), .. segments.Skip(first).Take(ChunkSize)];
            corrected.RemoveRange(corrected.Count - carried, carried);

            log($"Correcting segments {first + 1}-{end} of {segments.Count} with {model} ({EffortLevel} effort)");
            var message = UserMessage(corrected.TakeLast(ContextSize), chunk, segments.Skip(end).Take(ContextSize));
            var edits = ParseEdits(await send(systemPrompt, message, cancellationToken));
            var (result, applied) = SegmentEditor.Apply(chunk, edits);
            corrected.AddRange(result);
            changes.AddRange(applied);
        }

        // Measurements, not verdicts: what counts as too long or too short is the rules' call, and the reader's.
        var widths = corrected.Select(segment => Width(OneLine(segment))).ToList();
        var measured = corrected.Count == 0
            ? ""
            : string.Create(CultureInfo.InvariantCulture, $"; cue width {widths.Min()}-{widths.Max()} characters, longest {corrected.Max(Seconds):0.0} s");
        log($"Correction: {changes.Count} changes, {segments.Count} -> {corrected.Count} segments{measured}");
        return (corrected, changes);
    }

    private static string UserMessage(
        IEnumerable<TranscriptionSegment> before,
        IReadOnlyList<TranscriptionSegment> chunk,
        IEnumerable<TranscriptionSegment> after)
    {
        var message = new StringBuilder();
        AppendContext(message, "context_before", before);

        message.Append("<segments>\n");
        for (var i = 0; i < chunk.Count; i++)
        {
            // The model compares numbers well and counts characters badly, so the counting is done here.
            var text = OneLine(chunk[i]);
            message.Append(CultureInfo.InvariantCulture, $"{i + 1} [{Seconds(chunk[i]):0.0}s, width {Width(text)}] {text}\n");
        }

        message.Append("</segments>\n");
        AppendContext(message, "context_after", after);
        return message.ToString();
    }

    private static void AppendContext(StringBuilder message, string tag, IEnumerable<TranscriptionSegment> segments)
    {
        var lines = segments.Select(OneLine).ToList();
        if (lines.Count > 0)
        {
            message.Append(CultureInfo.InvariantCulture, $"<{tag}>\n{string.Join('\n', lines)}\n</{tag}>\n");
        }
    }

    private static string OneLine(TranscriptionSegment segment) => segment.Text.Trim().ReplaceLineEndings(" ");

    /// <summary>How long the cue is shown.</summary>
    private static double Seconds(TranscriptionSegment segment) => (segment.End - segment.Start).TotalSeconds;

    /// <summary>Display width in full-width characters: half-width letters, digits and katakana count as half.</summary>
    private static int Width(string text) =>
        (int)Math.Ceiling(text.Sum(character => character <= '\u00FF' || character is >= '\uFF61' and <= '\uFF9F' ? 0.5 : 1));

    private static List<SegmentEdit> ParseEdits(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True)
            {
                throw new InvalidOperationException(root.TryGetProperty("result", out var message) ? message.GetString() : "Claude Code reported an error");
            }

            if (!root.TryGetProperty("structured_output", out var output) || output.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Claude Code returned no edits");
            }

            // --json-schema makes the CLI validate this shape, so the properties below are always present.
            return output.GetProperty("edits").EnumerateArray()
                .Select(edit => new SegmentEdit(
                    edit.GetProperty("from").GetInt32(),
                    edit.GetProperty("to").GetInt32(),
                    edit.GetProperty("lines").EnumerateArray().Select(line => line.GetString() ?? "").ToList(),
                    Enum.Parse<ChangeKind>(edit.GetProperty("kind").GetString() ?? "", ignoreCase: true)))
                .ToList();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Claude Code returned an answer that is not JSON");
        }
    }

    private async Task<string> RunClaudeAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken)
    {
        // The prompt goes through a file and the message through stdin: with a glossary they exceed
        // the length Windows allows on a command line.
        var promptFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(promptFile, systemPrompt, cancellationToken);

            // --safe-mode keeps the user's own CLAUDE.md, hooks, plugins and MCP servers out of the run while their
            // sign-in still works (--bare would demand an API key). --tools "" leaves the model no tool, so it only
            // transforms text and the transcript cannot make it act.
            string[] arguments =
            [
                "-p", "--safe-mode", "--tools", "", "--model", model, "--effort", EffortLevel, "--no-session-persistence",
                "--output-format", "json", "--system-prompt-file", promptFile, "--json-schema", Schema,
            ];
            var (output, errors, exitCode) = await RunAsync(arguments, userMessage, ChunkTimeout, cancellationToken);

            // The CLI reports its own failures as JSON on stdout; an empty stdout means it did not get that far.
            return output.Length > 0
                ? output
                : throw new InvalidOperationException($"claude exited with code {exitCode}: {errors.Trim()}");
        }
        finally
        {
            File.Delete(promptFile);
        }
    }

    // shortcut: starts "claude" as an executable, so an npm install on Windows (claude.cmd) is not found, resolve through PATHEXT if users have one
    private static async Task<(string Output, string Errors, int ExitCode)> RunAsync(
        IEnumerable<string> arguments,
        string input,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("claude", arguments)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetTempPath(),
            CreateNoWindow = true,
        };

        Process? started;
        try
        {
            started = Process.Start(start);
        }
        catch (Win32Exception)
        {
            started = null;
        }

        using var process = started ?? throw new InvalidOperationException(
            "Claude Code is not installed: correction needs the `claude` command on PATH and signed in (https://claude.com/claude-code). Install it, or turn correction off.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var errors = process.StandardError.ReadToEndAsync(deadline.Token);
            try
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), deadline.Token);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // claude quit before reading its input (an older version rejects a flag, say); its exit code and stderr say why.
            }

            await process.WaitForExitAsync(deadline.Token);
            return (await output, await errors, process.ExitCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"claude did not answer within {timeout.TotalSeconds:0} seconds");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
