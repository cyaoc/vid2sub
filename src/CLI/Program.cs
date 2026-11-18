using System.CommandLine;
using Vid2Sub.Application;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Audio;
using Vid2Sub.Infrastructure.Configuration;
using Vid2Sub.Infrastructure.Files;
using Vid2Sub.Infrastructure.Speech;

namespace Vid2Sub.CLI;

/// <summary>Command-line entry point: parses arguments, wires the services together and reports results.</summary>
public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var inputs = new Argument<string[]>("input")
        {
            Description = "Input files, directories or wildcard patterns",
            Arity = ArgumentArity.OneOrMore
        };
        var outputDir = new Option<string?>("--output-dir", "-o") { Description = "Output directory (default: beside each input file)" };
        var format = new Option<OutputFormat?>("--format", "-f") { Description = "Output format" };
        var language = new Option<string?>("--language", "-l") { Description = "Recognition language: auto, zh, en, ja, ..." };
        var model = new Option<ModelType?>("--model", "-m") { Description = "Whisper model" };
        var configPath = new Option<string?>("--config", "-c") { Description = "Config file path (default: ./config.yaml, then beside the executable)" };
        var threads = new Option<int?>("--threads", "-t") { Description = "Processing threads (0 = one per CPU core)" };
        var verbose = new Option<bool>("--verbose", "-v") { Description = "Print settings and every recognized segment" };

        var root = new RootCommand("Vid2Sub - video/audio to subtitles, powered by Whisper.net")
        {
            inputs, outputDir, format, language, model, configPath, threads, verbose
        };

        root.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                var config = await YamlConfigLoader.LoadAsync(parseResult.GetValue(configPath), cancellationToken);
                config.ApplyOverrides(
                    parseResult.GetValue(format),
                    parseResult.GetValue(language),
                    parseResult.GetValue(model),
                    parseResult.GetValue(threads),
                    parseResult.GetValue(verbose));

                return await RunAsync(parseResult.GetValue(inputs) ?? [], parseResult.GetValue(outputDir), config, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Cancelled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return root.Parse(args).InvokeAsync();
    }

    private static async Task<int> RunAsync(
        string[] inputs,
        string? outputDir,
        AppConfiguration config,
        CancellationToken cancellationToken)
    {
        var (files, missing) = InputFileCollector.Collect(inputs);
        foreach (var input in missing)
        {
            Console.Error.WriteLine($"Warning: no media files found for '{input}'");
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("Error: no input files to process");
            return 1;
        }

        Action<TranscriptionSegment>? onSegment = null;
        if (config.Output.Verbose)
        {
            Console.WriteLine($"Model: {config.Model.Type}, language: {config.Inference.Language}, format: {config.Output.Format}");
            onSegment = segment => Console.WriteLine($"  [{Clock(segment.Start)} -> {Clock(segment.End)}] {segment.Text}");
        }

        var modelProvider = new WhisperModelProvider(
            config.Model.StorageDir,
            new ConsoleProgressBar($"Downloading {config.Model.Type} model"));
        await using var recognizer = new WhisperRecognizer(modelProvider, config, Console.WriteLine);
        var service = new TranscriptionService(new ManagedAudioProcessor(), recognizer);

        var claimedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failed = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var outputPath = TranscriptionService.OutputPathFor(file, outputDir, config.Output.Format);
            Console.WriteLine($"[{i + 1}/{files.Count}] {Path.GetFileName(file)}");
            try
            {
                if (!claimedOutputs.Add(outputPath))
                {
                    throw new InvalidOperationException($"another input already writes {outputPath}");
                }

                var count = await service.TranscribeAsync(file, outputPath, config.Output.Format, onSegment, cancellationToken);
                Console.WriteLine($"  -> {outputPath} ({count} segments)");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                Console.Error.WriteLine($"  Failed: {ex.Message}");
            }
        }

        Console.WriteLine($"Done: {files.Count - failed} succeeded, {failed} failed");
        return failed > 0 ? 1 : 0;
    }

    private static string Clock(TimeSpan time) => $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
}
