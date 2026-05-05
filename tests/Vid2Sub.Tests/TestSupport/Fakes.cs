using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Tests;

internal static class TestConfigurations
{
    public static ResolvedAppConfiguration Resolved(
        string? outputDir = null,
        bool overwrite = true)
    {
        var root = Path.GetTempPath();
        return new ResolvedAppConfiguration(
            new ResolvedModelConfiguration(ModelType.Tiny, Path.Combine(root, "models")),
            new ResolvedInferenceConfiguration("auto", 1, 5),
            new ResolvedEnvironmentConfiguration("ffmpeg", Path.Combine(root, "temp")),
            new ResolvedOutputConfiguration(OutputFormat.Vtt, Vid2SubLogLevel.Debug, outputDir, overwrite));
    }
}

internal sealed class FakeModelProvider : IModelProvider
{
    public int EnsureCalls { get; private set; }

    public Task<string> EnsureModelAsync(ModelType modelType, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureCalls++;
        progress?.Report(1.0);
        return Task.FromResult("/tmp/model.bin");
    }

    public bool ModelExists(ModelType modelType) => true;

    public string GetModelPath(ModelType modelType) => "/tmp/model.bin";
}

internal sealed class FakeAudioProcessor : IAudioProcessor
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<string> ConvertToWavAsync(string inputPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(inputPath + ".wav");

    public void CleanupTempFile(string wavPath)
    {
    }
}

internal sealed class FailingAudioProcessor : IAudioProcessor
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<string> ConvertToWavAsync(string inputPath, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ffmpeg failed");

    public void CleanupTempFile(string wavPath)
    {
    }
}

internal sealed class FakeRuntimeFactory(IReadOnlyList<TranscriptionSegment> segments) : IWhisperRuntimeFactory
{
    public Task<IWhisperRuntime> CreateAsync(string modelPath, ResolvedInferenceConfiguration inference, CancellationToken cancellationToken = default) =>
        Task.FromResult<IWhisperRuntime>(new FakeRuntime(segments));
}

internal sealed class FakeRuntime(IReadOnlyList<TranscriptionSegment> segments) : IWhisperRuntime
{
    public string RuntimeDescription => "fake-runtime";

    public IWhisperProcessor CreateProcessor() => new FakeProcessor(segments);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeProcessor(IReadOnlyList<TranscriptionSegment> segments) : IWhisperProcessor
{
    public async IAsyncEnumerable<TranscriptionSegment> ProcessAsync(
        Stream audioStream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        foreach (var segment in segments)
        {
            yield return segment;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeAudioContentReader : IAudioContentReader
{
    public Task<Stream> OpenReadAsync(string audioPath, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
}

internal sealed class RecordingSubtitleWriter : ISubtitleOutputWriter
{
    public List<(string Path, IReadOnlyList<TranscriptionSegment> Segments)> Writes { get; } = [];

    public Task WriteAsync(string outputPath, IReadOnlyList<TranscriptionSegment> segments, CancellationToken cancellationToken = default)
    {
        Writes.Add((outputPath, segments));
        return Task.CompletedTask;
    }
}

internal sealed class RecordingProgress : ITranscriptionProgress
{
    public List<TranscriptionProgressEvent> Events { get; } = [];

    public void Report(TranscriptionProgressEvent progressEvent) => Events.Add(progressEvent);
}
