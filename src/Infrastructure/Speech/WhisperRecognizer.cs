using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Vid2Sub.Infrastructure.Speech;

/// <summary>
/// Whisper.net-backed recognizer. The native runtime (CUDA, Vulkan, CoreML, CPU ...) is whatever
/// Whisper.net's autoprobe picks from the referenced runtime packages; this class never inspects
/// the OS or GPU. The model loads on first use and stays loaded until disposal.
/// </summary>
public sealed class WhisperRecognizer(IModelProvider modelProvider, AppConfiguration config, Action<string> log) : ISpeechRecognizer
{
    private WhisperFactory? _factory;
    private bool _useGpu = true;

    public async Task<IReadOnlyList<TranscriptionSegment>> RecognizeAsync(
        ReadOnlyMemory<float> samples,
        Action<TranscriptionSegment>? onSegment = null,
        CancellationToken cancellationToken = default)
    {
        var modelPath = await modelProvider.EnsureModelAsync(config.Model.Type, cancellationToken);
        try
        {
            return await RunAsync(modelPath, samples, onSegment, cancellationToken);
        }
        catch (Exception ex) when (_useGpu && ex is not OperationCanceledException)
        {
            // Out-of-memory GPUs and unsupported drivers fail with assorted native errors that cannot be
            // told apart reliably, so any failure on the accelerated path gets one retry on CPU.
            log($"Warning: Whisper failed on the accelerated runtime ({ex.Message}); retrying on CPU.");
            _useGpu = false;
            _factory?.Dispose();
            _factory = null;
            return await RunAsync(modelPath, samples, onSegment, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<TranscriptionSegment>> RunAsync(
        string modelPath,
        ReadOnlyMemory<float> samples,
        Action<TranscriptionSegment>? onSegment,
        CancellationToken cancellationToken)
    {
        _factory ??= await Task.Run(() => CreateFactory(modelPath), cancellationToken);

        var builder = _factory.CreateBuilder()
            .WithLanguage(config.Inference.Language)
            .WithThreads(config.Inference.Threads > 0 ? config.Inference.Threads : Environment.ProcessorCount)
            .WithBeamSearchSamplingStrategy(beamSearch => beamSearch.WithBeamSize(config.Inference.BeamSize));

        await using var processor = builder.Build();
        var segments = new List<TranscriptionSegment>();
        await foreach (var result in processor.ProcessAsync(samples, cancellationToken))
        {
            var segment = new TranscriptionSegment(result.Start, result.End, result.Text);
            segments.Add(segment);
            onSegment?.Invoke(segment);
        }

        return segments;
    }

    private WhisperFactory CreateFactory(string modelPath)
    {
        var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = _useGpu });
        log($"Whisper runtime: {RuntimeOptions.LoadedLibrary}{(_useGpu ? "" : " (GPU disabled)")}");
        return factory;
    }

    public ValueTask DisposeAsync()
    {
        _factory?.Dispose();
        _factory = null;
        return ValueTask.CompletedTask;
    }
}
