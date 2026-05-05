using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Application;

/// <summary>
/// Coordinates model provisioning, audio conversion, transcription, and subtitle output.
/// </summary>
public sealed class TranscriptionService(
    IModelProvider modelProvider,
    IAudioProcessor audioProcessor,
    IWhisperRuntimeFactory runtimeFactory,
    IAudioContentReader audioContentReader,
    ISubtitleOutputWriter subtitleOutputWriter,
    ITranscriptionProgress progress,
    ResolvedAppConfiguration config,
    ILogger<TranscriptionService>? logger = null) : IAsyncDisposable
{
    private IWhisperRuntime? _runtime;
    private bool _disposed;

    public async IAsyncEnumerable<TranscriptionResult> ProcessAsync(
        IEnumerable<TranscriptionWorkItem> workItems,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var items = workItems.ToList();
        if (items.Count == 0)
        {
            yield break;
        }

        progress.Report(new TranscriptionProgressEvent(
            TranscriptionProgressKind.BatchStarted,
            $"Processing {items.Count} file(s)"));

        string? modelPath = null;
        TranscriptionError? initializationError = null;
        try
        {
            modelPath = await modelProvider.EnsureModelAsync(
                config.Model.Type,
                progress: null,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Model provisioning failed for {ModelType}", config.Model.Type);
            initializationError = new TranscriptionError(
                TranscriptionStage.ModelProvisioning,
                TranscriptionErrorCodes.ModelProvisioningFailed,
                ex.Message,
                config.Model.StorageDir);
        }

        if (initializationError is not null)
        {
            foreach (var item in items)
            {
                yield return Failed(item.SourcePath, initializationError);
            }

            yield break;
        }

        try
        {
            _runtime ??= await runtimeFactory.CreateAsync(
                modelPath!,
                config.Inference,
                cancellationToken);

            progress.Report(new TranscriptionProgressEvent(
                TranscriptionProgressKind.RuntimeReady,
                $"Whisper runtime: {_runtime.RuntimeDescription}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Whisper runtime initialization failed for model path {ModelPath}", modelPath);
            initializationError = new TranscriptionError(
                TranscriptionStage.RuntimeInitialization,
                TranscriptionErrorCodes.RuntimeInitializationFailed,
                ex.Message,
                modelPath);
        }

        if (initializationError is not null)
        {
            foreach (var item in items)
            {
                yield return Failed(item.SourcePath, initializationError);
            }

            yield break;
        }

        foreach (var item in items)
        {
            yield return await ProcessSingleAsync(item, _runtime!, cancellationToken);
        }
    }

    private async Task<TranscriptionResult> ProcessSingleAsync(
        TranscriptionWorkItem item,
        IWhisperRuntime runtime,
        CancellationToken cancellationToken)
    {
        var stage = TranscriptionStage.AudioConversion;
        string? wavPath = null;

        progress.Report(new TranscriptionProgressEvent(
            TranscriptionProgressKind.FileStarted,
            $"Processing {Path.GetFileName(item.SourcePath)}",
            item.SourcePath));

        try
        {
            wavPath = await audioProcessor.ConvertToWavAsync(item.SourcePath, cancellationToken);

            stage = TranscriptionStage.Transcription;
            var segments = new List<TranscriptionSegment>();

            await using (var audioStream = await audioContentReader.OpenReadAsync(wavPath, cancellationToken))
            await using (var processor = runtime.CreateProcessor())
            {
                await foreach (var segment in processor.ProcessAsync(audioStream, cancellationToken))
                {
                    segments.Add(segment);
                }
            }

            stage = TranscriptionStage.SubtitleWriting;
            await subtitleOutputWriter.WriteAsync(item.OutputPath, segments, cancellationToken);

            progress.Report(new TranscriptionProgressEvent(
                TranscriptionProgressKind.FileCompleted,
                $"Completed {Path.GetFileName(item.SourcePath)}",
                item.SourcePath));

            return new TranscriptionResult(
                item.SourcePath,
                segments,
                TranscriptionStatus.Success,
                Error: null,
                Language: config.Inference.Language == "auto" ? null : config.Inference.Language);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Transcription failed at {Stage} for {SourcePath}",
                stage,
                item.SourcePath);
            var error = ErrorForStage(stage, ex, item, wavPath);
            progress.Report(new TranscriptionProgressEvent(
                TranscriptionProgressKind.FileFailed,
                error.Message,
                item.SourcePath));

            return new TranscriptionResult(
                item.SourcePath,
                [],
                TranscriptionStatus.Failed,
                error);
        }
        finally
        {
            if (!string.IsNullOrEmpty(wavPath))
            {
                audioProcessor.CleanupTempFile(wavPath);
            }
        }
    }

    private static TranscriptionResult Failed(
        string sourcePath,
        TranscriptionStage stage,
        string code,
        string message,
        string? path) =>
        new(
            sourcePath,
            [],
            TranscriptionStatus.Failed,
            new TranscriptionError(stage, code, message, path));

    private static TranscriptionResult Failed(string sourcePath, TranscriptionError error) =>
        new(sourcePath, [], TranscriptionStatus.Failed, error);

    private static TranscriptionError ErrorForStage(
        TranscriptionStage stage,
        Exception ex,
        TranscriptionWorkItem item,
        string? wavPath)
    {
        var (code, path) = stage switch
        {
            TranscriptionStage.AudioConversion => (TranscriptionErrorCodes.AudioConversionFailed, item.SourcePath),
            TranscriptionStage.Transcription => (TranscriptionErrorCodes.TranscriptionFailed, wavPath),
            TranscriptionStage.SubtitleWriting => (TranscriptionErrorCodes.OutputWriteFailed, item.OutputPath),
            _ => (TranscriptionErrorCodes.TranscriptionFailed, item.SourcePath)
        };

        return new TranscriptionError(stage, code, ex.Message, path);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_runtime is not null)
        {
            await _runtime.DisposeAsync();
            _runtime = null;
        }

        await audioProcessor.DisposeAsync();
    }
}
