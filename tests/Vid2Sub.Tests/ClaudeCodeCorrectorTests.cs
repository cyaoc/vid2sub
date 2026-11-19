using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Correction;

namespace Vid2Sub.Tests;

public class ClaudeCodeCorrectorTests
{
    private const string NoEdits = """{"is_error":false,"structured_output":{"edits":[]}}""";

    private static (string Text, string Source) Rules(string? language) => ("RULES", "test rules");

    [Fact]
    public async Task Edits_from_the_cli_are_applied_and_reported()
    {
        var log = new List<string>();
        string? systemPrompt = null, message = null;
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, _ => (" RULES\n", "test rules"), "サイボウズ\nkintone", log.Add, (prompt, input, _) =>
        {
            (systemPrompt, message) = (prompt, input);
            return Task.FromResult("""{"is_error":false,"structured_output":{"edits":[{"from":1,"to":2,"lines":["サイボウズの行動指針"],"kind":"term"},{"from":3,"to":3,"lines":[],"kind":"hallucination"}]}}""");
        });

        var (result, changes) = await corrector.CorrectAsync(
            [Segment(0, 2, " 再棒図の"), Segment(2, 4.5, "行動指針"), Segment(9, 10, "ご視聴ありがとうございました")],
            TestContext.Current.CancellationToken);

        Assert.Equal([Segment(0, 4.5, "サイボウズの行動指針")], result);
        Assert.Contains("<rules>\nRULES\n</rules>\n\n<glossary>\nサイボウズ\nkintone\n</glossary>", systemPrompt);
        Assert.Equal("<segments>\n1 [2.0s, width 4] 再棒図の\n2 [2.5s, width 4] 行動指針\n3 [1.0s, width 14] ご視聴ありがとうございました\n</segments>\n", message);
        Assert.Contains("Correction: 2 changes, 3 -> 1 segments; cue width 10-10 characters, longest 4.5 s", log);
        Assert.Equal([ChangeKind.Term, ChangeKind.Hallucination], changes.Select(change => change.Kind));
        Assert.Equal(["再棒図の", "行動指針"], changes[0].Before);
        Assert.Equal(["サイボウズの行動指針"], changes[0].After);
        Assert.Equal(TimeSpan.FromSeconds(9), changes[1].Start);
        Assert.Empty(changes[1].After);
    }

    [Fact]
    public async Task Long_transcripts_are_sent_in_chunks_numbered_from_one_with_their_neighbours_as_context()
    {
        var messages = new List<string>();
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, Rules, "", _ => { }, run: (_, input, _) =>
        {
            messages.Add(input);
            // The same edit in every chunk: it must land on that chunk's own first cue, which in the second
            // chunk is the first of the cues carried over from the chunk before.
            return Task.FromResult("""{"structured_output":{"edits":[{"from":1,"to":1,"lines":["first"],"kind":"guess"}]}}""");
        });
        var segments = Enumerable.Range(1, 152).Select(i => Segment(i, i + 1, $"s{i}")).ToList();

        var (result, changes) = await corrector.CorrectAsync(segments, TestContext.Current.CancellationToken);

        Assert.Equal(2, messages.Count);
        Assert.EndsWith("150 [1.0s, width 2] s150\n</segments>\n<context_after>\ns151\ns152\n</context_after>\n", messages[0]);
        Assert.Equal(
            "<context_before>\ns141\ns142\ns143\ns144\ns145\n</context_before>\n<segments>\n"
            + "1 [1.0s, width 2] s146\n2 [1.0s, width 2] s147\n3 [1.0s, width 2] s148\n4 [1.0s, width 2] s149\n5 [1.0s, width 2] s150\n"
            + "6 [1.0s, width 2] s151\n7 [1.0s, width 2] s152\n</segments>\n",
            messages[1]);
        Assert.Equal(152, result.Count);
        Assert.Equal(["first", "s2"], result.Take(2).Select(segment => segment.Text));
        Assert.Equal(["s145", "first", "s147", "s148", "s149", "s150", "s151", "s152"], result.Skip(144).Select(segment => segment.Text));
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(146)], changes.Select(change => change.Start));
    }

    [Fact]
    public async Task Cues_that_a_chunk_edge_cut_apart_can_be_merged()
    {
        var calls = 0;
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, Rules, "", _ => { }, run: (_, _, _) => Task.FromResult(++calls == 1
            ? NoEdits
            // Cue 150 comes back as number 5 of the second chunk, where cue 151 is number 6.
            : """{"structured_output":{"edits":[{"from":5,"to":6,"lines":["s150s151"],"kind":"segmentation"}]}}"""));
        var segments = Enumerable.Range(1, 151).Select(i => Segment(i, i + 1, $"s{i}")).ToList();

        var (result, changes) = await corrector.CorrectAsync(segments, TestContext.Current.CancellationToken);

        Assert.Equal([.. segments.Take(149), Segment(150, 152, "s150s151")], result);
        Assert.Equal(["s150", "s151"], Assert.Single(changes).Before);
    }

    [Theory]
    [InlineData("""{"is_error":true,"result":"Not logged in"}""", "Not logged in")]
    [InlineData("""{"is_error":false,"structured_output":null}""", "no edits")]
    [InlineData("not json", "not JSON")]
    public async Task An_answer_without_edits_fails_the_correction(string response, string expectedMessage)
    {
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, Rules, "", _ => { }, run: (_, _, _) => Task.FromResult(response));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => corrector.CorrectAsync([Segment(0, 1, "one")], TestContext.Current.CancellationToken));

        Assert.Contains(expectedMessage, failure.Message);
    }

    [Fact]
    public async Task A_failure_in_a_later_chunk_fails_the_whole_correction()
    {
        var calls = 0;
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, Rules, "", _ => { }, (_, _, _) => ++calls == 1
            ? Task.FromResult("""{"structured_output":{"edits":[{"from":1,"to":1,"lines":["fixed"],"kind":"guess"}]}}""")
            : Task.FromException<string>(new TimeoutException("claude did not answer")));
        var segments = Enumerable.Range(1, 151).Select(i => Segment(i, i + 1, $"s{i}")).ToList();

        await Assert.ThrowsAsync<TimeoutException>(() => corrector.CorrectAsync(segments, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("""{"loggedIn":true,"authMethod":"claude.ai"}""")]
    [InlineData("Logged in as someone")] // a version that does not answer in JSON is given the benefit of the doubt
    public async Task A_signed_in_claude_passes_the_check(string status) =>
        await ClaudeCodeCorrector.EnsureSignedInAsync(_ => Task.FromResult(status), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_signed_out_claude_fails_the_check()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ClaudeCodeCorrector.EnsureSignedInAsync(_ => Task.FromResult("""{"loggedIn":false}"""), TestContext.Current.CancellationToken));

        Assert.Contains("not signed in", failure.Message);
    }

    [Fact]
    public async Task A_missing_claude_fails_the_check() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ClaudeCodeCorrector.EnsureSignedInAsync(
                _ => Task.FromException<string>(new InvalidOperationException("Claude Code is not installed")),
                TestContext.Current.CancellationToken));

    [Fact]
    public async Task Cancellation_is_not_swallowed_as_a_failed_correction()
    {
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, Rules, "", _ => { }, run: (_, _, cancellationToken) =>
            Task.FromCanceled<string>(new CancellationToken(canceled: true)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => corrector.CorrectAsync([Segment(0, 1, "one")], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Nothing_is_sent_for_an_empty_transcript()
    {
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, Rules, "", _ => { }, run: (_, _, _) => throw new InvalidOperationException("must not run"));

        Assert.Empty((await corrector.CorrectAsync([], TestContext.Current.CancellationToken)).Segments);
    }

    [Theory]
    // The language the user named wins; without one, the rules go by what Whisper recognized.
    [InlineData("zh", "ja", "zh")]
    [InlineData(null, "ja", "ja")]
    [InlineData(null, null, null)]
    public async Task Rules_are_put_together_for_the_language_of_the_subtitles(string? named, string? recognized, string? expected)
    {
        var log = new List<string>();
        string? asked = "not asked", systemPrompt = null;
        var corrector = new ClaudeCodeCorrector(
            "test-model",
            CorrectionEffort.Medium,
            language =>
            {
                asked = language;
                return ($"RULES FOR {language ?? "any language"}", "common + its own");
            },
            "",
            log.Add,
            (prompt, _, _) =>
            {
                systemPrompt = prompt;
                return Task.FromResult(NoEdits);
            },
            named);

        await corrector.CorrectAsync(
            [new TranscriptionSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "one", recognized)],
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, asked);
        Assert.Contains($"<rules>\nRULES FOR {expected ?? "any language"}\n</rules>", systemPrompt);
        Assert.Contains("Correction rules: common + its own", log);
    }

    [Fact]
    public async Task Half_width_characters_count_as_half_a_character_of_width()
    {
        string? message = null;
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, Rules, "", _ => { }, run: (_, input, _) =>
        {
            message = input;
            return Task.FromResult(NoEdits);
        });

        // 17 full-width characters and 21 half-width ones: 38 characters that read as 27.5.
        await corrector.CorrectAsync([Segment(0, 5.4, "英語表記はBehavioral Guidelinesという名前にしております")], TestContext.Current.CancellationToken);

        Assert.Contains("1 [5.4s, width 28] 英語表記は", message);
    }

    [Fact]
    public async Task Without_a_glossary_the_prompt_has_no_glossary_block()
    {
        string? systemPrompt = null;
        var corrector = new ClaudeCodeCorrector("test-model", CorrectionEffort.Medium, Rules, " ", _ => { }, run: (prompt, _, _) =>
        {
            systemPrompt = prompt;
            return Task.FromResult(NoEdits);
        });

        await corrector.CorrectAsync([Segment(0, 1, "one")], TestContext.Current.CancellationToken);

        Assert.EndsWith("<rules>\nRULES\n</rules>\n", systemPrompt);
    }

    private static TranscriptionSegment Segment(double start, double end, string text) =>
        new(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), text);
}
