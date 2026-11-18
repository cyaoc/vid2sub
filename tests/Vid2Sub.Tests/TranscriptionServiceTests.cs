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

    private sealed class FakeAudio(float[]? samples) : IAudioProcessor
    {
        public Task<ReadOnlyMemory<float>> DecodeAsync(string inputPath, CancellationToken cancellationToken = default) =>
            samples is null
                ? Task.FromException<ReadOnlyMemory<float>>(new NotSupportedException("unsupported"))
                : Task.FromResult<ReadOnlyMemory<float>>(samples);
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
