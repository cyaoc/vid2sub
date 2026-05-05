using Vid2Sub.Application;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Tests.Application;

public sealed class TranscriptionServiceTests
{
    [Fact]
    public async Task ProcessAsync_ProvisionsModelOnceAndReportsRuntime()
    {
        var modelProvider = new FakeModelProvider();
        var runtimeFactory = new FakeRuntimeFactory([new(TimeSpan.Zero, TimeSpan.FromSeconds(1), "hello")]);
        var progress = new RecordingProgress();
        var subtitleWriter = new RecordingSubtitleWriter();

        await using var service = new TranscriptionService(
            modelProvider,
            new FakeAudioProcessor(),
            runtimeFactory,
            new FakeAudioContentReader(),
            subtitleWriter,
            progress,
            TestConfigurations.Resolved());

        var results = await service.ProcessAsync([
            new TranscriptionWorkItem("one.mp4", "one.vtt"),
            new TranscriptionWorkItem("two.mp4", "two.vtt")
        ]).ToListAsync();

        Assert.Equal(1, modelProvider.EnsureCalls);
        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(TranscriptionStatus.Success, result.Status));
        Assert.Contains(progress.Events, e => e.Kind == TranscriptionProgressKind.RuntimeReady && e.Message.Contains("fake-runtime"));
        Assert.Equal(2, subtitleWriter.Writes.Count);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsFailedResultWithStage()
    {
        await using var service = new TranscriptionService(
            new FakeModelProvider(),
            new FailingAudioProcessor(),
            new FakeRuntimeFactory([]),
            new FakeAudioContentReader(),
            new RecordingSubtitleWriter(),
            new RecordingProgress(),
            TestConfigurations.Resolved());

        var result = await service.ProcessAsync([new TranscriptionWorkItem("bad.mp4", "bad.vtt")]).SingleAsync();

        Assert.Equal(TranscriptionStatus.Failed, result.Status);
        Assert.Equal(TranscriptionStage.AudioConversion, result.Error?.Stage);
    }
}
