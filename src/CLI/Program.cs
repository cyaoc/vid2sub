using System.CommandLine;
using Vid2Sub.Application;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;
using Vid2Sub.Domain.Services;
using Vid2Sub.Infrastructure.Audio;
using Vid2Sub.Infrastructure.Configuration;
using Vid2Sub.Infrastructure.Correction;
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
            Description = "Media files, directories or wildcard patterns; an .srt or .vtt file is proofread without transcribing",
            Arity = ArgumentArity.OneOrMore
        };
        var outputDir = new Option<string?>("--output-dir", "-o") { Description = "Output directory (default: beside each input file)" };
        var format = new Option<OutputFormat?>("--format", "-f") { Description = "Output format" };
        var language = new Option<string?>("--language", "-l") { Description = "Recognition language: auto, zh, en, ja, ..." };
        var model = new Option<ModelType?>("--model", "-m") { Description = "Whisper model" };
        var configPath = new Option<string?>("--config", "-c") { Description = "Config file path (default: ./config.yaml, then beside the executable)" };
        var threads = new Option<int?>("--threads", "-t") { Description = "Processing threads (0 = one per CPU core)" };
        var verbose = new Option<bool>("--verbose", "-v") { Description = "Print settings and every recognized segment" };
        var glossary = new Option<string?>("--glossary", "-g") { Description = "Proofread with the local Claude Code CLI, using this term list (.xlsx or text)" };
        var glossaryColumn = new Option<string?>("--glossary-column", "-G") { Description = "Header of the glossary column in the language of the subtitles, such as ja or zh" };

        var root = new RootCommand("Vid2Sub - video/audio to subtitles, powered by Whisper.net")
        {
            inputs, outputDir, format, language, model, configPath, threads, verbose, glossary, glossaryColumn
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
                    parseResult.GetValue(verbose),
                    parseResult.GetValue(glossary) is { } glossaryPath ? Path.GetFullPath(glossaryPath) : null,
                    parseResult.GetValue(glossaryColumn));

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
            onSegment = segment => Console.WriteLine($"  [{SubtitleFormatter.Clock(segment.Start)} -> {SubtitleFormatter.Clock(segment.End)}] {segment.Text}");
        }

        var modelProvider = new WhisperModelProvider(
            config.Model.StorageDir,
            new ConsoleProgressBar($"Downloading {config.Model.Type} model"));
        await using var recognizer = new WhisperRecognizer(modelProvider, config, Console.WriteLine);

        ISubtitleCorrector? corrector = null;
        var read = files;
        if (config.Correction.Enabled)
        {
            // Checked up front: a wrong glossary path or a missing Claude Code should fail now, not after the first transcription.
            IReadOnlyList<string> terms = [];
            if (!string.IsNullOrWhiteSpace(config.Correction.Glossary))
            {
                // Always shown: it is the only way to see that the program read the column the user meant.
                terms = GlossaryReader.Read(config.Correction.Glossary, config.Correction.GlossaryColumn);
                var column = string.IsNullOrWhiteSpace(config.Correction.GlossaryColumn) ? "" : $", column \"{config.Correction.GlossaryColumn}\"";
                Console.WriteLine($"Glossary: {terms.Count} terms from {Path.GetFileName(config.Correction.Glossary)}{column}");

                // The glossary is read by this run too: a text glossary beside an input of the same name must not become its output.
                read = [.. files, config.Correction.Glossary];
            }

            await ClaudeCodeCorrector.EnsureSignedInAsync(cancellationToken: cancellationToken);
            if (config.Output.Verbose)
            {
                Console.WriteLine($"Correction: {config.Correction.Model}, {config.Correction.Effort} effort");
            }

            corrector = new ClaudeCodeCorrector(
                config.Correction.Model,
                config.Correction.Effort,
                language => CorrectionRules.Load(AppContext.BaseDirectory, language),
                string.Join('\n', terms),
                Console.WriteLine,
                language: string.Equals(config.Inference.Language, "auto", StringComparison.OrdinalIgnoreCase) ? null : config.Inference.Language);
        }

        var service = new TranscriptionService(new ManagedAudioProcessor(), recognizer, corrector, read);

        var failed = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var outputPath = TranscriptionService.OutputPathFor(file, outputDir, config.Output.Format);
            Console.WriteLine($"[{i + 1}/{files.Count}] {Path.GetFileName(file)}");
            try
            {
                var count = await service.TranscribeAsync(file, outputPath, config.Output.Format, onSegment, cancellationToken);
                Console.WriteLine($"  -> {outputPath} ({count} segments)");
                if (corrector is not null)
                {
                    Console.WriteLine($"     corrections to review: {TranscriptionService.ChangesPathFor(outputPath)}");
                }
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
}
