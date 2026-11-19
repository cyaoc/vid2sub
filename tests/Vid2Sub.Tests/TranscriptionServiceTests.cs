using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Vid2Sub.Application;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Tests;

public class TranscriptionServiceTests
{
    private static readonly TranscriptionSegment Segment = new(TimeSpan.Zero, TimeSpan.FromSeconds(1), "hello");

    [Fact]
    public async Task Decoded_audio_is_recognized_and_written_as_a_subtitle_file()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "new-dir", "clip.srt");
        var recognizer = new FakeRecognizer([Segment]);
        var service = new TranscriptionService(new FakeAudio([0.25f, -0.25f]), recognizer);
        var reported = new List<TranscriptionSegment>();

        var count = await service.TranscribeAsync("clip.mp4", output, OutputFormat.Srt, reported.Add, TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal([0.25f, -0.25f], recognizer.Heard);
        Assert.Equal([Segment], reported);
        Assert.Contains("00:00:00,000 --> 00:00:01,000", await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_corrector_decides_which_segments_are_written()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "clip.txt");
        var corrector = new FakeCorrector([Segment with { Text = "corrected" }, Segment with { Text = "and split" }]);
        var service = new TranscriptionService(new FakeAudio([0.25f]), new FakeRecognizer([Segment]), corrector);

        var count = await service.TranscribeAsync("clip.mp4", output, OutputFormat.Text, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
        Assert.Equal([Segment], corrector.Received);
        Assert.Equal(["corrected", "and split"], await File.ReadAllLinesAsync(output, TestContext.Current.CancellationToken));
        Assert.Contains("hello\n     => corrected | and split", (await File.ReadAllTextAsync(Path.Combine(temp.Path, "clip.changes.txt"), TestContext.Current.CancellationToken)).ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task An_existing_subtitle_file_is_corrected_without_decoding_or_recognizing()
    {
        using var temp = new TempDirectory();
        var input = temp.File("clip.srt", "1\n00:00:00,000 --> 00:00:01,000\nhello\n");
        var output = Path.Combine(temp.Path, "out", "clip.srt");
        var corrector = new FakeCorrector([Segment with { Text = "corrected" }]);
        var service = new TranscriptionService(new FakeAudio(null), new FakeRecognizer([]), corrector);

        var count = await service.TranscribeAsync(input, output, OutputFormat.Srt, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal([Segment], corrector.Received);
        Assert.Contains("00:00:00,000 --> 00:00:01,000\ncorrected", (await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task A_subtitle_file_is_never_overwritten_by_its_own_result()
    {
        using var temp = new TempDirectory();
        var input = temp.File("clip.srt", "1\n00:00:00,000 --> 00:00:01,000\nhello\n");
        var service = new TranscriptionService(new FakeAudio(null), new FakeRecognizer([]), new FakeCorrector([Segment]));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TranscribeAsync(input, input, OutputFormat.Srt, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("overwrite the input", failure.Message);
        Assert.Contains("hello", await File.ReadAllTextAsync(input, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_result_never_replaces_an_input_the_batch_has_yet_to_read()
    {
        using var temp = new TempDirectory();
        var first = temp.File("clip.srt", "1\n00:00:00,000 --> 00:00:01,000\nfrom the srt\n");
        var second = temp.File("clip.vtt", "WEBVTT\n\n00:00:00.000 --> 00:00:01.000\nfrom the vtt\n");
        var service = new TranscriptionService(new FakeAudio(null), new FakeRecognizer([]), inputs: [first, second]);

        // Converting clip.srt to WebVTT would write clip.vtt, which is the second input and has not been read yet.
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TranscribeAsync(first, second, OutputFormat.Vtt, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("overwrite the input", failure.Message);
        Assert.Contains("from the vtt", await File.ReadAllTextAsync(second, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]  // the output folder is a second name for the folder of the input
    [InlineData(false)] // the input is a link to the very file the result would be written to
    public async Task A_second_name_for_the_same_file_does_not_get_past_the_input_guard(bool folderAlias)
    {
        using var temp = new TempDirectory();
        var real = temp.File("real/clip.srt", "1\n00:00:00,000 --> 00:00:01,000\nthe original\n");
        var input = Path.Combine(temp.Path, folderAlias ? Path.Combine("alias", "clip.srt") : "link.srt");
        SymbolicLink(() =>
        {
            if (folderAlias)
            {
                Directory.CreateSymbolicLink(Path.Combine(temp.Path, "alias"), Path.Combine(temp.Path, "real"));
            }
            else
            {
                File.CreateSymbolicLink(input, real);
            }
        });

        var service = new TranscriptionService(new FakeAudio(null), new FakeRecognizer([]));

        // The two paths are spelled differently but name one file, so comparing them as text is not enough.
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TranscribeAsync(input, real, OutputFormat.Vtt, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("overwrite the input", failure.Message);
        Assert.Contains("the original", await File.ReadAllTextAsync(real, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_subtitles_and_their_correction_list_are_never_one_file()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "clip.vtt");
        // Left behind by something else: the subtitle path is a link to where the list of corrections goes.
        SymbolicLink(() => File.CreateSymbolicLink(output, Path.Combine(temp.Path, "clip.changes.txt")));

        var service = new TranscriptionService(new FakeAudio([0.25f]), new FakeRecognizer([Segment]), new FakeCorrector([Segment]));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TranscribeAsync("clip.mp4", output, OutputFormat.Vtt, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("would both be written to", failure.Message);
        Assert.False(File.Exists(Path.Combine(temp.Path, "clip.changes.txt")));
    }

    [Theory]
    // Every input owns two files in the same folder: its subtitles and its list of corrections, which is
    // written with correction on and removed with it off. Whichever of these two inputs comes second wants a
    // file the first one already owns.
    [InlineData("clip.mp4", "clip.changes.mp4", true)]
    [InlineData("clip.changes.mp4", "clip.mp4", true)]
    [InlineData("clip.mp4", "clip.changes.mp4", false)]
    [InlineData("clip.changes.mp4", "clip.mp4", false)]
    public async Task Subtitles_and_correction_lists_of_different_inputs_never_share_a_file(string first, string second, bool correct)
    {
        using var temp = new TempDirectory();
        var service = new TranscriptionService(new FakeAudio([0.25f]), new FakeRecognizer([Segment]), correct ? new FakeCorrector([Segment]) : null);
        string Output(string input) => TranscriptionService.OutputPathFor(Path.Combine(temp.Path, input), null, OutputFormat.Text);

        await service.TranscribeAsync(first, Output(first), OutputFormat.Text, cancellationToken: TestContext.Current.CancellationToken);
        var written = Directory.GetFiles(temp.Path).ToDictionary(file => file, File.ReadAllText);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TranscribeAsync(second, Output(second), OutputFormat.Text, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("another input already writes", failure.Message);
        Assert.Equal(written, Directory.GetFiles(temp.Path).ToDictionary(file => file, File.ReadAllText));
    }

    [Fact]
    public async Task A_hard_link_to_the_input_is_replaced_and_the_input_keeps_its_text()
    {
        using var temp = new TempDirectory();
        var input = temp.File("clip.srt", "1\n00:00:00,000 --> 00:00:01,000\nthe original\n");
        var output = Path.Combine(temp.Path, "clip.vtt");
        var created = OperatingSystem.IsWindows() ? CreateHardLinkW(output, input, 0) : link(input, output) == 0;
        Assert.SkipUnless(created, "hard links cannot be created here");
        var service = new TranscriptionService(new FakeAudio(null), new FakeRecognizer([]));

        // Two names of one file, which no comparison of paths can tell: written in place, the result would
        // replace the text of the input as well.
        await service.TranscribeAsync(input, output, OutputFormat.Vtt, cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("1\n00:00:00,000 --> 00:00:01,000\nthe original", await File.ReadAllTextAsync(input, TestContext.Current.CancellationToken));
        Assert.StartsWith("WEBVTT", await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
        Assert.Equal([input, output], Directory.GetFiles(temp.Path).Order()); // nothing half-written is left behind
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task A_replaced_output_keeps_its_permissions()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows keeps permissions in access control lists, not in a file mode");
        }

        using var temp = new TempDirectory();
        var output = temp.File("clip.txt", "an earlier run");
        const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        File.SetUnixFileMode(output, ownerOnly);
        var service = new TranscriptionService(new FakeAudio([0.25f]), new FakeRecognizer([Segment]));

        await service.TranscribeAsync("clip.mp4", output, OutputFormat.Text, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["hello"], await File.ReadAllLinesAsync(output, TestContext.Current.CancellationToken));
        Assert.Equal(ownerOnly, File.GetUnixFileMode(output));
    }

    [Fact]
    public async Task An_output_name_near_the_length_limit_is_written()
    {
        using var temp = new TempDirectory();
        // 250 characters where file systems allow 255: no room for a suffix, and the name of its list of
        // corrections would be too long, which matters even without correction, when an old list is looked for.
        var output = Path.Combine(temp.Path, new string('a', 246) + ".txt");
        var service = new TranscriptionService(new FakeAudio([0.25f]), new FakeRecognizer([Segment]));

        await service.TranscribeAsync("clip.mp4", output, OutputFormat.Text, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["hello"], await File.ReadAllLinesAsync(output, TestContext.Current.CancellationToken));
        Assert.Equal([output], Directory.GetFiles(temp.Path));
    }

    [Fact]
    public async Task An_output_that_is_a_symbolic_link_writes_the_file_it_points_at()
    {
        using var temp = new TempDirectory();
        var real = Path.Combine(temp.Path, "published", "clip.txt");
        var output = Path.Combine(temp.Path, "clip.txt");
        SymbolicLink(() => File.CreateSymbolicLink(output, real));
        var service = new TranscriptionService(new FakeAudio([0.25f]), new FakeRecognizer([Segment]));

        await service.TranscribeAsync("clip.mp4", output, OutputFormat.Text, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["hello"], await File.ReadAllLinesAsync(real, TestContext.Current.CancellationToken));
        Assert.NotNull(new FileInfo(output).LinkTarget);
    }

    [Fact]
    public async Task Subtitles_written_without_correction_do_not_keep_the_correction_list_of_an_earlier_run()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "clip.txt");
        var staleChanges = temp.File("clip.changes.txt", "corrections of an earlier run");
        var service = new TranscriptionService(new FakeAudio([0.25f]), new FakeRecognizer([Segment]));

        await service.TranscribeAsync("clip.mp4", output, OutputFormat.Text, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["hello"], await File.ReadAllLinesAsync(output, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(staleChanges));
    }

    [Fact]
    public async Task A_file_without_cues_is_reported_instead_of_written_empty()
    {
        using var temp = new TempDirectory();
        var input = temp.File("notes.vtt", "WEBVTT\n\nNOTE nothing here\n");
        var service = new TranscriptionService(new FakeAudio(null), new FakeRecognizer([]));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.TranscribeAsync(input, Path.Combine(temp.Path, "notes.srt"), OutputFormat.Srt, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_failed_correction_still_writes_the_recognized_subtitles_and_then_fails()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "clip.txt");
        var staleChanges = temp.File("clip.changes.txt", "corrections of an earlier run");
        var service = new TranscriptionService(new FakeAudio([0.25f]), new FakeRecognizer([Segment]), new FakeCorrector(null));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TranscribeAsync("clip.mp4", output, OutputFormat.Text, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("out of quota", failure.Message);
        Assert.Contains("written without correction", failure.Message);
        Assert.Equal(["hello"], await File.ReadAllLinesAsync(output, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(staleChanges));
    }

    [Fact]
    public async Task Failed_decode_leaves_no_subtitle_file()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "clip.vtt");
        var service = new TranscriptionService(new FakeAudio(null), new FakeRecognizer([Segment]));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.TranscribeAsync("clip.mp4", output, OutputFormat.Vtt, cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData(null, OutputFormat.Vtt, "videos/clip.vtt")]
    [InlineData("", OutputFormat.Srt, "videos/clip.srt")]
    [InlineData("out", OutputFormat.Text, "out/clip.txt")]
    public void Output_goes_beside_the_input_unless_a_directory_is_given(string? outputDir, OutputFormat format, string expected) =>
        Assert.Equal(
            expected.Replace('/', Path.DirectorySeparatorChar),
            TranscriptionService.OutputPathFor(Path.Combine("videos", "clip.mp4"), outputDir, format));

    /// <summary>Skips the test where symbolic links cannot be made, such as Windows without developer mode.</summary>
    private static void SymbolicLink(Action create)
    {
        try
        {
            create();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"symbolic links cannot be created here: {ex.Message}");
        }
    }

    // .NET has no call that makes a hard link.
    [DllImport("libc")]
    private static extern int link(string existingPath, string newPath);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLinkW(string newPath, string existingPath, nint securityAttributes);

    private sealed class FakeAudio(float[]? samples) : IAudioProcessor
    {
        public Task<ReadOnlyMemory<float>> DecodeAsync(string inputPath, CancellationToken cancellationToken = default) =>
            samples is null
                ? Task.FromException<ReadOnlyMemory<float>>(new NotSupportedException("unsupported"))
                : Task.FromResult<ReadOnlyMemory<float>>(samples);
    }

    /// <summary>Returns <paramref name="corrected"/>, or fails when it is null.</summary>
    private sealed class FakeCorrector(IReadOnlyList<TranscriptionSegment>? corrected) : ISubtitleCorrector
    {
        public IReadOnlyList<TranscriptionSegment> Received { get; private set; } = [];

        public Task<(IReadOnlyList<TranscriptionSegment> Segments, IReadOnlyList<SegmentChange> Changes)> CorrectAsync(
            IReadOnlyList<TranscriptionSegment> segments,
            CancellationToken cancellationToken = default)
        {
            Received = segments;
            if (corrected is null)
            {
                throw new InvalidOperationException("out of quota");
            }

            // One change that replaces everything, so the report has something to show.
            IReadOnlyList<SegmentChange> changes =
                [new(segments[0].Start, ChangeKind.Guess, [.. segments.Select(segment => segment.Text)], [.. corrected.Select(segment => segment.Text)])];
            return Task.FromResult((corrected, changes));
        }
    }

    private sealed class FakeRecognizer(IReadOnlyList<TranscriptionSegment> segments) : ISpeechRecognizer
    {
        public float[] Heard { get; private set; } = [];

        public Task<IReadOnlyList<TranscriptionSegment>> RecognizeAsync(
            ReadOnlyMemory<float> samples,
            Action<TranscriptionSegment>? onSegment = null,
            CancellationToken cancellationToken = default)
        {
            Heard = samples.ToArray();
            foreach (var segment in segments)
            {
                onSegment?.Invoke(segment);
            }

            return Task.FromResult(segments);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
