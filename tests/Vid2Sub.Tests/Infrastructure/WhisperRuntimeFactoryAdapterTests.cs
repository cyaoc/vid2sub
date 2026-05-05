using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Models;

namespace Vid2Sub.Tests.Infrastructure;

public sealed class WhisperRuntimeFactoryAdapterTests
{
    [Fact]
    public async Task CreateProcessor_AppliesBeamSizeFromResolvedInference()
    {
        var builder = new RecordingWhisperBuilder();
        var facade = new RecordingWhisperFactoryFacade(builder);
        var factory = new WhisperRuntimeFactoryAdapter(facade);

        await using var runtime = await factory.CreateAsync(
            "/tmp/model.bin",
            new ResolvedInferenceConfiguration("en", 2, 9));
        await using var processor = runtime.CreateProcessor();

        Assert.Equal("en", builder.Language);
        Assert.Equal(2, builder.Threads);
        Assert.Equal(9, builder.BeamSize);
    }

    private sealed class RecordingWhisperFactoryFacade(RecordingWhisperBuilder builder) : IWhisperFactoryFacade
    {
        public IWhisperFactoryHandle FromPath(string modelPath, bool useGpu) => new RecordingWhisperFactoryHandle(builder);
    }

    private sealed class RecordingWhisperFactoryHandle(RecordingWhisperBuilder builder) : IWhisperFactoryHandle
    {
        public string RuntimeDescription => "recording-runtime";

        public IWhisperBuilderFacade CreateBuilder() => builder;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingWhisperBuilder : IWhisperBuilderFacade
    {
        public string? Language { get; private set; }

        public int? Threads { get; private set; }

        public int? BeamSize { get; private set; }

        public IWhisperBuilderFacade WithLanguage(string language)
        {
            Language = language;
            return this;
        }

        public IWhisperBuilderFacade WithThreads(int threads)
        {
            Threads = threads;
            return this;
        }

        public IWhisperBuilderFacade WithBeamSize(int beamSize)
        {
            BeamSize = beamSize;
            return this;
        }

        public IWhisperNativeProcessor Build() => new RecordingNativeProcessor();
    }

    private sealed class RecordingNativeProcessor : IWhisperNativeProcessor
    {
        public async IAsyncEnumerable<TranscriptionSegment> ProcessAsync(
            Stream audioStream,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
