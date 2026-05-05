using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;
using Vid2Sub.Application;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Audio;
using Vid2Sub.Infrastructure.Configuration;
using Vid2Sub.Infrastructure.Files;
using Vid2Sub.Infrastructure.Models;
using Vid2Sub.Infrastructure.Subtitles;

namespace Vid2Sub.CLI;

/// <summary>
/// Vid2Sub command-line entry point.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Vid2Sub - Video/audio to subtitle tool powered by Whisper.net");

        var inputArgument = new Argument<string[]>("input")
        {
            Description = "Input file(s), directory path(s), or wildcard pattern(s)",
            Arity = ArgumentArity.OneOrMore
        };

        var outputOption = new Option<string?>("--output-dir", "-o")
        {
            Description = "Output directory (default: same as input file)"
        };

        var formatOption = new Option<string?>("--format", "-f")
        {
            Description = "Output format: srt, vtt, text"
        };

        var languageOption = new Option<string?>("--language", "-l")
        {
            Description = "Recognition language: auto, zh, en, ja, etc."
        };

        var modelOption = new Option<string?>("--model", "-m")
        {
            Description = "Model type: Tiny, Base, Small, Medium, LargeV3, LargeV3Turbo"
        };

        var configOption = new Option<string?>("--config", "-c")
        {
            Description = "Specify config file path"
        };

        var threadsOption = new Option<int?>("--threads", "-t")
        {
            Description = "Number of processing threads (0 = processor count)"
        };

        var logLevelOption = new Option<string?>("--log-level")
        {
            Description = "Log level: quiet, error, warning, information, debug"
        };

        var overwriteOption = new Option<bool>("--overwrite")
        {
            Description = "Overwrite existing subtitle files"
        };

        rootCommand.Arguments.Add(inputArgument);
        rootCommand.Options.Add(outputOption);
        rootCommand.Options.Add(formatOption);
        rootCommand.Options.Add(languageOption);
        rootCommand.Options.Add(modelOption);
        rootCommand.Options.Add(configOption);
        rootCommand.Options.Add(threadsOption);
        rootCommand.Options.Add(logLevelOption);
        rootCommand.Options.Add(overwriteOption);

        rootCommand.SetAction(parseResult =>
        {
            var cliOptions = new CliOptions(
                Inputs: parseResult.GetValue(inputArgument) ?? [],
                OutputDir: parseResult.GetValue(outputOption),
                Format: parseResult.GetValue(formatOption),
                Language: parseResult.GetValue(languageOption),
                Model: parseResult.GetValue(modelOption),
                ConfigPath: parseResult.GetValue(configOption),
                Threads: parseResult.GetValue(threadsOption),
                LogLevel: parseResult.GetValue(logLevelOption),
                Overwrite: parseResult.GetValue(overwriteOption));

            try
            {
                return RunAsync(cliOptions, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Operation cancelled");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static async Task<int> RunAsync(
        CliOptions cliOptions,
        CancellationToken cancellationToken)
    {
        var resolver = new ConfigurationResolver(
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory);

        var configResult = await resolver.ResolveAsync(
            cliOptions.ConfigPath,
            cliOptions,
            cancellationToken);

        if (!configResult.IsSuccess)
        {
            foreach (var error in configResult.Errors)
            {
                Console.Error.WriteLine($"{error.Key}: {error.Message}");
            }

            return 1;
        }

        var config = configResult.Configuration!;
        PrintStartup(config, cliOptions);

        var collector = new InputFileCollector();
        var collectedInputs = collector.Collect(cliOptions.Inputs);

        var outputPlanner = new OutputPlanner();
        var outputPlan = outputPlanner.CreatePlan(collectedInputs, config);

        var outcomes = new List<TranscriptionResult>(outputPlan.Outcomes);
        if (outputPlan.WorkItems.Count > 0)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(ToMicrosoftLogLevel(config.Output.LogLevel))
                    .AddConsole();
            });

            var modelProvider = new WhisperModelProvider(config.Model);
            var audioProcessor = new FFmpegAudioProcessor(config.Environment);
            var runtimeFactory = new WhisperRuntimeFactoryAdapter();
            var audioContentReader = new FileAudioContentReader();
            var subtitleWriter = new SubtitleOutputWriter(config.Output.Format);
            var progress = new ConsoleTranscriptionProgress(config.Output.LogLevel);
            var logger = loggerFactory.CreateLogger<TranscriptionService>();

            await using var transcriptionService = new TranscriptionService(
                modelProvider,
                audioProcessor,
                runtimeFactory,
                audioContentReader,
                subtitleWriter,
                progress,
                config,
                logger);

            await foreach (var result in transcriptionService.ProcessAsync(outputPlan.WorkItems, cancellationToken))
            {
                outcomes.Add(result);
            }
        }

        PrintOutcomes(outcomes, config.Output.LogLevel);

        return outcomes.Any(outcome => outcome.Status == TranscriptionStatus.Failed) ? 1 : 0;
    }

    private static void PrintStartup(ResolvedAppConfiguration config, CliOptions cliOptions)
    {
        if (config.Output.LogLevel is Vid2SubLogLevel.Quiet or Vid2SubLogLevel.Error)
        {
            return;
        }

        Console.WriteLine("Vid2Sub - Video/Audio to Subtitle Tool");
        Console.WriteLine($"Inputs: {cliOptions.Inputs.Count}");
        Console.WriteLine($"Model: {config.Model.Type}");
        Console.WriteLine($"Language: {config.Inference.Language}");
        Console.WriteLine($"Output format: {config.Output.Format}");
        Console.WriteLine();
    }

    private static void PrintOutcomes(
        IReadOnlyList<TranscriptionResult> outcomes,
        Vid2SubLogLevel logLevel)
    {
        var succeeded = outcomes.Count(outcome => outcome.Status == TranscriptionStatus.Success);
        var failed = outcomes.Count(outcome => outcome.Status == TranscriptionStatus.Failed);
        var skipped = outcomes.Count(outcome => outcome.Status == TranscriptionStatus.Skipped);

        foreach (var outcome in outcomes.Where(outcome => outcome.Status != TranscriptionStatus.Success))
        {
            var stream = outcome.Status == TranscriptionStatus.Failed ? Console.Error : Console.Out;
            stream.WriteLine($"{outcome.Status}: {outcome.SourceFile}");
            if (outcome.Error is not null && logLevel != Vid2SubLogLevel.Quiet)
            {
                stream.WriteLine($"  [{outcome.Error.Stage}/{outcome.Error.Code}] {outcome.Error.Message}");
            }
        }

        if (logLevel != Vid2SubLogLevel.Quiet)
        {
            Console.WriteLine();
            Console.WriteLine($"Processing complete: {succeeded} succeeded, {failed} failed, {skipped} skipped");
        }
    }

    private static LogLevel ToMicrosoftLogLevel(Vid2SubLogLevel logLevel) => logLevel switch
    {
        Vid2SubLogLevel.Quiet => LogLevel.None,
        Vid2SubLogLevel.Error => LogLevel.Error,
        Vid2SubLogLevel.Warning => LogLevel.Warning,
        Vid2SubLogLevel.Information => LogLevel.Information,
        Vid2SubLogLevel.Debug => LogLevel.Debug,
        _ => LogLevel.Information
    };
}
