using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Vid2Sub.Infrastructure.Models;

/// <summary>
/// Default Whisper.net-backed runtime factory.
/// </summary>
public sealed class WhisperRuntimeFactoryAdapter : IWhisperRuntimeFactory
{
    private readonly IWhisperFactoryFacade _facade;

    public WhisperRuntimeFactoryAdapter()
        : this(new WhisperFactoryFacade())
    {
    }

    public WhisperRuntimeFactoryAdapter(IWhisperFactoryFacade facade)
    {
        _facade = facade;
    }

    public async Task<IWhisperRuntime> CreateAsync(
        string modelPath,
        ResolvedInferenceConfiguration inference,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run<IWhisperRuntime>(() =>
        {
            try
            {
                return new WhisperRuntimeAdapter(_facade.FromPath(modelPath, useGpu: true), inference);
            }
            catch (Exception gpuException)
            {
                try
                {
                    return new WhisperRuntimeAdapter(_facade.FromPath(modelPath, useGpu: false), inference);
                }
                catch (Exception cpuException)
                {
                    throw new InvalidOperationException(
                        $"Whisper runtime initialization failed. GPU error: {gpuException.Message}. CPU fallback error: {cpuException.Message}",
                        cpuException);
                }
            }
        }, cancellationToken);
    }

    private sealed class WhisperRuntimeAdapter(
        IWhisperFactoryHandle factory,
        ResolvedInferenceConfiguration inference) : IWhisperRuntime
    {
        public string RuntimeDescription => factory.RuntimeDescription;

        public IWhisperProcessor CreateProcessor()
        {
            var builder = factory.CreateBuilder()
                .WithLanguage(inference.Language)
                .WithThreads(inference.EffectiveThreads)
                .WithBeamSize(inference.BeamSize);

            return new WhisperProcessorAdapter(builder.Build());
        }

        public ValueTask DisposeAsync()
        {
            return factory.DisposeAsync();
        }
    }

    private sealed class WhisperProcessorAdapter(IWhisperNativeProcessor processor) : IWhisperProcessor
    {
        public async IAsyncEnumerable<TranscriptionSegment> ProcessAsync(
            Stream audioStream,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var result in processor.ProcessAsync(audioStream, cancellationToken))
            {
                yield return result;
            }
        }

        public ValueTask DisposeAsync() => processor.DisposeAsync();
    }

    private sealed class WhisperFactoryFacade : IWhisperFactoryFacade
    {
        public IWhisperFactoryHandle FromPath(string modelPath, bool useGpu)
        {
            var factory = useGpu
                ? WhisperFactory.FromPath(modelPath)
                : WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = false });

            return new WhisperFactoryHandle(factory);
        }
    }

    private sealed class WhisperFactoryHandle(WhisperFactory factory) : IWhisperFactoryHandle
    {
        public string RuntimeDescription => RuntimeOptions.LoadedLibrary?.ToString() ?? "Unknown";

        public IWhisperBuilderFacade CreateBuilder() => new WhisperBuilderFacade(factory.CreateBuilder());

        public ValueTask DisposeAsync()
        {
            factory.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class WhisperBuilderFacade(WhisperProcessorBuilder builder) : IWhisperBuilderFacade
    {
        public IWhisperBuilderFacade WithLanguage(string language)
        {
            builder.WithLanguage(language);
            return this;
        }

        public IWhisperBuilderFacade WithThreads(int threads)
        {
            builder.WithThreads(threads);
            return this;
        }

        public IWhisperBuilderFacade WithBeamSize(int beamSize)
        {
            if (builder.WithBeamSearchSamplingStrategy() is not BeamSearchSamplingStrategyBuilder beamSearch)
            {
                throw new InvalidOperationException("Whisper.net did not create a beam search sampling builder.");
            }

            beamSearch.WithBeamSize(beamSize);
            return this;
        }

        public IWhisperNativeProcessor Build() => new WhisperNativeProcessor(builder.Build());
    }

    private sealed class WhisperNativeProcessor(WhisperProcessor processor) : IWhisperNativeProcessor
    {
        public async IAsyncEnumerable<TranscriptionSegment> ProcessAsync(
            Stream audioStream,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var result in processor.ProcessAsync(audioStream, cancellationToken))
            {
                yield return new TranscriptionSegment(result.Start, result.End, result.Text);
            }
        }

        public ValueTask DisposeAsync()
        {
            processor.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
